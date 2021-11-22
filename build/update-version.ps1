function Set-VsixVersion {
  Param(
    [string]
    $ManifestPath,
    [string]
    $Version
  )
  
  $FullPath = Resolve-Path ("$PSScriptRoot\..\src\$ManifestPath")
  Write-Host "Setting version to $Version in file $FullPath"
  [xml]$content = Get-Content $FullPath
  $content.PackageManifest.Metadata.Identity.Version = $Version
  $content.Save($FullPath)
}

$Version = $args[0]
Write-Host "Set version: $Version"

Set-VsixVersion -ManifestPath "Switchyard.Vsix\source.extension.vsixmanifest" -Version $Version
Set-VsixVersion -ManifestPath "Switchyard.Vsix.22\source.extension.vsixmanifest" -Version $Version