$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tool = Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/AIQuotaBar.CodexTrace.exe'
$root = Join-Path $repo ('artifacts/hotfix-codex-trace/analysis-tests-' + [Guid]::NewGuid().ToString('N'))
$epoch = [DateTime]::SpecifyKind([DateTime]'2026-01-01T00:00:00', [DateTimeKind]::Utc)
function Time([double] $seconds) { $epoch.AddSeconds($seconds).ToString('o') }
function StartRow([int] $pidValue, [int] $parent, [double] $at) {
    @{ Kind='Start'; Process=@{ Pid=$pidValue; ParentPid=$parent; StartedUtc=(Time $at); Name='fixture.exe'; Rundown=$false } }
}
function FileRow([int] $pidValue, [double] $at, [string] $area, [string] $operation, [string] $relative) {
    @{ Kind='File'; File=@{ AtUtc=(Time $at); Pid=$pidValue; Tid=1000; Area=$area; Operation=$operation; RelativePath=$relative } }
}
function NameRow([double] $at, [int] $identity, [string] $kind, [string] $transition, [string] $area, [string] $relative='') {
    @{Kind='Name';Name=@{AtUtc=(Time $at);Identity=$identity;IdentityKind=$kind;Transition=$transition;Area=$area;RelativePath=$relative}}
}
function UnknownRow([int] $fileObject=701, [int] $fileKey=702) {
    @{Kind='Unknown';Unknown=@{AtUtc=(Time 2.2);Pid=202;Tid=1000;Operation='Write';FileObject=$fileObject;FileKey=$fileKey}}
}
$baseRows=@(
    (StartRow 101 10 1), (StartRow 102 101 1.1),
    (FileRow 102 1.2 'SelfTest' 'CreateOrOpen:Create' 'marker.txt'),
    (FileRow 102 1.3 'SelfTest' 'Write' 'marker.txt'),
    (FileRow 102 1.4 'SelfTest' 'Delete' 'delete-marker.txt'),
    (StartRow 201 10 2), (StartRow 202 201 2.1), (StartRow 300 10 2.1)
)
$cases=@(
    @{Name='resolved_helper'; Expected=$true; Extra=@(); Lost=0; Folders=@()},
    @{Name='unresolved_helper_io'; Expected=$false; Extra=@(@{Kind='Unknown';Unknown=@{AtUtc=(Time 2.2);Pid=202;Operation='Write'}}); Lost=0;Folders=@()},
    @{Name='unresolved_other_io'; Expected=$true; Extra=@(@{Kind='Unknown';Unknown=@{AtUtc=(Time 2.2);Pid=300;Operation='Write'}}); Lost=0;Folders=@()},
    @{Name='unknown_writer'; Expected=$false; Extra=@(@{Kind='Unknown';Unknown=@{AtUtc=(Time 2.2);Pid=-1;Operation='Write'}});Lost=0;Folders=@()},
    @{Name='missing_writer_lifetime'; Expected=$false; Extra=@(@{Kind='Unknown';Unknown=@{AtUtc=(Time 2.2);Pid=404;Operation='Write'}});Lost=0;Folders=@()},
    @{Name='lost_events'; Expected=$false; Extra=@();Lost=1;Folders=@()},
    @{Name='unattributed_new_folder'; Expected=$false;Extra=@();Lost=0;Folders=@('marketplace-upgrade-fixture')},
    @{Name='attributed_new_folder'; Expected=$true;Extra=@((FileRow 202 2.2 'Staging' 'CreateOrOpen:Create' 'marketplace-upgrade-fixture'));Lost=0;Folders=@('marketplace-upgrade-fixture')},
    @{Name='open_cannot_attribute_creation'; Expected=$false;Extra=@((FileRow 202 2.2 'Staging' 'CreateOrOpen:Open' 'marketplace-upgrade-fixture'));Lost=0;Folders=@('marketplace-upgrade-fixture')},
    @{Name='mapped_object_write';Expected=$true;Extra=@((NameRow 2.15 701 'Object' 'Create' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='mapped_key_write';Expected=$true;Extra=@((NameRow 2.15 702 'Key' 'Create' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='future_name_rejected';Expected=$false;Extra=@((NameRow 2.25 701 'Object' 'Create' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='closed_object_rejected';Expected=$false;Extra=@((NameRow 2.12 701 'Object' 'Create' 'OutsideStaging'),(NameRow 2.15 701 'Object' 'End' 'Unknown'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='conflicting_names_rejected';Expected=$false;Extra=@((NameRow 2.12 701 'Object' 'Create' 'OutsideStaging'),(NameRow 2.15 702 'Key' 'Create' 'Staging' 'marketplace-upgrade-fixture'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='mapped_staging_write';Expected=$true;Extra=@((NameRow 2.15 701 'Object' 'Create' 'Staging' 'marketplace-upgrade-fixture'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='stable_key_end_rundown';Expected=$true;Extra=@((NameRow 2.3 702 'Key' 'Rundown' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='rundown_reuse_rejected';Expected=$false;Extra=@((NameRow 2.3 702 'Key' 'Create' 'OutsideStaging'),(NameRow 2.4 702 'Key' 'Rundown' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='conflicting_rundown_rejected';Expected=$false;Extra=@((NameRow 2.3 702 'Key' 'Rundown' 'OutsideStaging'),(NameRow 2.4 702 'Key' 'Rundown' 'Staging' 'marketplace-upgrade-fixture'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='rundown_with_lost_events_rejected';Expected=$false;Extra=@((NameRow 2.3 702 'Key' 'Rundown' 'OutsideStaging'),(UnknownRow));Lost=1;Folders=@()},
    @{Name='initial_key_closure';Expected=$true;Extra=@((NameRow 2.3 702 'Key' 'End' 'OutsideStaging'),(NameRow 2.4 702 'Key' 'Create' 'Staging' 'marketplace-upgrade-fixture'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='closed_key_gap_rejected';Expected=$false;Extra=@((NameRow 2.1 702 'Key' 'End' 'OutsideStaging'),(NameRow 2.4 702 'Key' 'Create' 'OutsideStaging'),(UnknownRow));Lost=0;Folders=@()},
    @{Name='unknown_initial_closure_rejected';Expected=$false;Extra=@((NameRow 2.3 702 'Key' 'End' 'Unknown'),(UnknownRow));Lost=0;Folders=@()}
)
foreach($case in $cases) {
    $dir=Join-Path $root $case.Name
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    @{Baseline=@();InitialFolders=@()} | ConvertTo-Json -Depth 5 | Set-Content "$dir/capture-start.json"
    @{EventsLost=$case.Lost;Reason='DurationCompleted';FinalFolders=$case.Folders} | ConvertTo-Json -Depth 5 | Set-Content "$dir/capture-end.json"
    @{Pid=101;StartedUtc=(Time 1);ExitCode=0} | ConvertTo-Json | Set-Content "$dir/self-test.json"
    @{Pid=201;StartedUtc=(Time 2)} | ConvertTo-Json | Set-Content "$dir/probe-root.json"
    @($baseRows + $case.Extra) | ForEach-Object { ConvertTo-Json $_ -Depth 5 -Compress } | Set-Content "$dir/events.jsonl"
    & $tool analyze $dir | Out-Null
    $actual=Get-Content "$dir/summary.json" -Raw | ConvertFrom-Json
    if($actual.HelperAttributionConclusive -ne $case.Expected) { throw ('Analysis gate mismatch: ' + $case.Name) }
    if($case.Name -eq 'unresolved_other_io' -and $actual.AttributionConclusive) { throw 'Global uncertainty was incorrectly hidden.' }
    if($case.Name -eq 'attributed_new_folder' -and $actual.ProbeStagingEventCount -ne 1) { throw 'Owned file attribution failed.' }
    if($case.Name -eq 'mapped_staging_write' -and ($actual.ProbeStagingEventCount -ne 1 -or $actual.ResolvedByObjectNames -ne 1)) { throw 'Mapped staging attribution failed.' }
    Write-Output ('PASS: ' + $case.Name)
}
Write-Output 'Synthetic analysis checks only; runtime kernel capture still requires its self-test.'
