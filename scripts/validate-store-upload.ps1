[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $UploadPath,
    [string] $SourceManifest,
    [string] $ExpectedAppVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if ([string]::IsNullOrWhiteSpace($SourceManifest)) {
    $SourceManifest = Join-Path $PSScriptRoot '..\src\AIQuotaBar.Package\Package.appxmanifest'
}

function Read-ZipEntryText([System.IO.Compression.ZipArchive] $Archive, [string] $Name) {
    $entries = @($Archive.Entries | Where-Object FullName -eq $Name)
    if ($entries.Count -ne 1) { throw "Expected exactly one archive entry '$Name'." }
    $entry = $entries[0]
    if ($entry.Length -gt 1MB) { throw 'Manifest exceeds the 1 MB safety limit.' }
    $reader = [System.IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Read-SafeXml([string] $Text) {
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $document = [System.Xml.XmlDocument]::new()
    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]::new($Text), $settings)
    try { $document.Load($reader) } finally { $reader.Dispose() }
    return $document
}

function Normalize-Version([string] $Version) {
    $v = [System.Version]::Parse($Version)
    return '{0}.{1}.{2}.{3}' -f $v.Major, $v.Minor, $v.Build, $(if ($v.Revision -lt 0) { 0 } else { $v.Revision })
}

function Normalize-Path([string] $Path) { return $Path.Replace('/', '\').Trim() }

if (-not (Test-Path -LiteralPath $UploadPath -PathType Leaf)) { throw "UploadPath does not identify a file." }
if (-not (Test-Path -LiteralPath $SourceManifest -PathType Leaf)) { throw "SourceManifest does not identify a file." }
$upload = [System.IO.Path]::GetFullPath($UploadPath)
$source = [System.IO.Path]::GetFullPath($SourceManifest)
$maxBytes = 512MB
if ((Get-Item -LiteralPath $upload).Length -gt $maxBytes) { throw 'Upload archive exceeds the 512 MB safety limit.' }

$sourceXml = Read-SafeXml (Get-Content -LiteralPath $source -Raw)
$ns = [System.Xml.XmlNamespaceManager]::new($sourceXml.NameTable)
$ns.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
$sourcePackage = $sourceXml.SelectSingleNode('/f:Package', $ns)
$sourceIdentity = $sourcePackage.SelectSingleNode('f:Identity', $ns)
$sourceApp = $sourcePackage.SelectSingleNode('f:Applications/f:Application', $ns)
$sourceStartupExtension = $sourceApp.SelectSingleNode('f:Extensions/desktop:Extension[@Category="windows.startupTask"]', $ns)
$sourceStartup = $sourceStartupExtension.SelectSingleNode('desktop:StartupTask', $ns)
if ($null -eq $sourceIdentity -or $null -eq $sourceApp -or $null -eq $sourceStartupExtension -or $null -eq $sourceStartup) { throw 'Source manifest is missing required identity, application, or startup-task data.' }

if ([string]::IsNullOrWhiteSpace($ExpectedAppVersion)) {
    $appProject = Join-Path (Split-Path $source -Parent) '..\AIQuotaBar.App\AIQuotaBar.App.csproj'
    if (Test-Path -LiteralPath $appProject) {
        $versionMatch = [regex]::Match((Get-Content -LiteralPath $appProject -Raw), '<Version>\s*([^<]+)\s*</Version>')
        if ($versionMatch.Success) { $ExpectedAppVersion = Normalize-Version $versionMatch.Groups[1].Value.Trim() }
    }
} else { $ExpectedAppVersion = Normalize-Version $ExpectedAppVersion }

$outer = $null
$inner = $null
$payloadStream = $null
try {
    $outer = [System.IO.Compression.ZipFile]::OpenRead($upload)
    $msixEntries = @($outer.Entries | Where-Object { $_.FullName -match '(?i)\.msix$' })
    if ($msixEntries.Count -ne 1) { throw "Upload must contain exactly one .msix payload; found $($msixEntries.Count)." }
    $payload = $msixEntries[0]
    if ($payload.Length -gt $maxBytes) { throw 'Nested .msix payload exceeds the 512 MB safety limit.' }
    $payloadStream = [System.IO.MemoryStream]::new()
    $inputStream = $payload.Open()
    try {
        $buffer = [byte[]]::new(65536)
        while (($count = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
            if ($payloadStream.Length + $count -gt $maxBytes) { throw 'Nested .msix payload exceeds the 512 MB safety limit.' }
            $payloadStream.Write($buffer, 0, $count)
        }
    } finally { $inputStream.Dispose() }
    $payloadStream.Position = 0
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $payloadHash = [BitConverter]::ToString($sha.ComputeHash($payloadStream)).Replace('-', '') } finally { $sha.Dispose() }
    $payloadStream.Position = 0
    $inner = [System.IO.Compression.ZipArchive]::new($payloadStream, [System.IO.Compression.ZipArchiveMode]::Read, $true)
    $packagedXml = Read-SafeXml (Read-ZipEntryText $inner 'AppxManifest.xml')
    $packagedNs = [System.Xml.XmlNamespaceManager]::new($packagedXml.NameTable)
    $packagedNs.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $packagedNs.AddNamespace('desktop', 'http://schemas.microsoft.com/appx/manifest/desktop/windows10')
    $package = $packagedXml.SelectSingleNode('/f:Package', $packagedNs)
    $identity = $package.SelectSingleNode('f:Identity', $packagedNs)
    $app = $package.SelectSingleNode('f:Applications/f:Application', $packagedNs)
    $startupExtension = $app.SelectSingleNode('f:Extensions/desktop:Extension[@Category="windows.startupTask"]', $packagedNs)
    $startup = $startupExtension.SelectSingleNode('desktop:StartupTask', $packagedNs)
    if ($null -eq $identity -or $null -eq $app -or $null -eq $startupExtension -or $null -eq $startup) { throw 'Payload manifest is missing required identity, application, or startup-task data.' }

    function Assert-Same([string] $Name, [object] $Expected, [object] $Actual) {
        if ($Expected -ne $Actual) { throw "Manifest mismatch in ${Name}: source '$Expected', payload '$Actual'." }
    }
    Assert-Same 'IdentityName' $sourceIdentity.Name $identity.Name
    Assert-Same 'Publisher' $sourceIdentity.Publisher $identity.Publisher
    Assert-Same 'Version' $sourceIdentity.Version $identity.Version
    Assert-Same 'ProcessorArchitecture' $sourceIdentity.ProcessorArchitecture $identity.ProcessorArchitecture
    Assert-Same 'ApplicationId' $sourceApp.Id $app.Id
    Assert-Same 'Executable' (Normalize-Path $sourceApp.Executable) (Normalize-Path $app.Executable)
    Assert-Same 'EntryPoint' $sourceApp.EntryPoint $app.EntryPoint
    Assert-Same 'StartupExecutable' (Normalize-Path $sourceStartupExtension.Executable) (Normalize-Path $startupExtension.Executable)
    Assert-Same 'StartupEntryPoint' $sourceStartupExtension.EntryPoint $startupExtension.EntryPoint
    Assert-Same 'StartupTaskId' $sourceStartup.TaskId $startup.TaskId
    Assert-Same 'StartupTaskEnabled' $sourceStartup.Enabled $startup.Enabled
    Assert-Same 'StartupTaskDisplayName' $sourceStartup.DisplayName $startup.DisplayName
    if ($ExpectedAppVersion -and $identity.Version -ne $ExpectedAppVersion) { throw "Payload version '$($identity.Version)' does not match expected app version '$ExpectedAppVersion'." }
    $leaf = [System.IO.Path]::GetFileName($upload)
    if ($leaf -notmatch ('_' + [regex]::Escape($identity.Version) + '_')) { throw "Upload filename '$leaf' does not contain package version '$($identity.Version)'." }
    [ordered]@{ Upload = $leaf; UploadSHA256 = (Get-FileHash -LiteralPath $upload).Hash; Msix = $payload.FullName; MsixSHA256 = $payloadHash; PackageVersion = $identity.Version; ExpectedAppVersion = $ExpectedAppVersion; Result = 'Valid' } | ConvertTo-Json -Compress
} finally {
    if ($null -ne $inner) { $inner.Dispose() }
    if ($null -ne $payloadStream) { $payloadStream.Dispose() }
    if ($null -ne $outer) { $outer.Dispose() }
}
