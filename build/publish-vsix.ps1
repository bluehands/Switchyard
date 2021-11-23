function Publish-Extension {
  Param(
    [string]
    $VsixPath,
    [string]
    $ManifestPath,
    [string]
    $PersonalAccessToken
  )
  
  # Find the location of VsixPublisher
  $Installation = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -format json | ConvertFrom-Json
  $Path = $Installation.installationPath

  Write-Host $Path
  $VsixPublisher = Join-Path -Path $Path -ChildPath "VSSDK\VisualStudioIntegration\Tools\Bin\VsixPublisher.exe" -Resolve

  Write-Host $VsixPublisher

  # Publish to VSIX to the marketplace
  & $VsixPublisher publish -payload $VsixPath -publishManifest $ManifestPath -personalAccessToken $PersonalAccessToken -ignoreWarnings "VSIXValidatorWarning01,VSIXValidatorWarning02,VSIXValidatorWarning08"
}


$PersonalAccessToken = $args[0]
$ManifestPath = "$PSScriptRoot\extension-manifest.json"

Publish-Extension -VsixPath "$PSScriptRoot\..\src\Switchyard.Vsix\bin\Release\net472\Switchyard.vsix" -ManifestPath $ManifestPath -PersonalAccessToken $PersonalAccessToken
Publish-Extension -VsixPath "$PSScriptRoot\..\src\Switchyard.Vsix.22\bin\Release\net472\Switchyard.vsix" -ManifestPath $ManifestPath -PersonalAccessToken $PersonalAccessToken