Properties {
	    $script:hash = @{}
	    $script:hash.build_mode = "Release"
	$solution = (ls *.sln).Name
	$packageName = [System.IO.Path]::GetFileNameWithoutExtension((ls *.sln))
	# Test
	$testPrj = "..\..\tests\"

	# Directories
	# Directory of output binaries (output of Visual Studio)
	$outdir = if (test-path env:CCNetArtifactDirectory) {[System.String]::Concat((ls env:CCNetArtifactDirectory).Value,"\Staging\\")} else {[System.String]::Concat((pwd),"\Staging\\")}
	$artifactdir = [System.String]::Concat((pwd),"\Artifacts\")
	$deployPackageDir = (join-path $outdir "..\DeployPackage")
}

Task default -depends Print-TaskList 

Task setConfiguration-Debug {
	$script:hash.build_mode = "Debug"
}

Task setConfiguration-Release {
	$script:hash.build_mode = "Release"
}

Task Print-Banner {
Write-Host -ForegroundColor Yellow "============================================="
Write-Host -ForegroundColor Yellow "Project: Redlock-cs"
Write-Host -ForegroundColor Yellow "Distributed lock with Redis and C#"
Write-Host -ForegroundColor Yellow "Author: Angelo Simone Scotto"
Write-Host -ForegroundColor Yellow "Url: https://github.com/KidFashion/redlock-cs"
Write-Host -ForegroundColor Yellow "============================================="

}

Task Print-TaskList -depends Print-Banner {
Write-Host -ForegroundColor White "List of Available Tasks:"
Write-Host -ForegroundColor Green "Print-TaskList" -nonewline;Write-Host " : Print these instructions."
#Write-Host -ForegroundColor Green "Build-Solution"-nonewline; write-host " : Build Project (4.5)"
#Write-Host -ForegroundColor Green "Build-Solution-net40"-nonewline; write-host " : Build Project (4.0)"


Write-Host -ForegroundColor Green "Build-Project"-nonewline; write-host " : Build Project (4.5)"
#Write-Host -ForegroundColor Green "Build-Project-Net45"-nonewline; write-host " : Build Project (4.5)"
Write-Host -ForegroundColor Green "Build-Project-Net40"-nonewline; write-host " : Build Project (4.0)"
Write-Host -ForegroundColor Green "Test-Solution" -nonewline; write-host " : Test Projects"
Write-Host -ForegroundColor Green "Measure-CodeCoverage" -nonewline; write-host " : Generate code coverage report."
Write-Host -ForegroundColor Green "Generate-Reports" -nonewline; write-host " : Generate UnitTest and CodeCoverage reports."

Write-Host -ForegroundColor Green "Package-Project" -nonewline; write-host " : Package Project in Nuget Package"
}


#Task Generate-Reports  -depends Test-Solution, Measure-CodeCoverage { 
#}

Task Build-Project -depends Build-Project-Net45 {
}

Task Build-Project-Net45 {
$configuration = $script:hash.build_mode
$version ="v4.5"
$folder = ".\src"
push-location $folder
$itemToBuild = ls -Filter *.csproj | where {$_.Name -match ".*\."+$version+".csproj"}
if ($itemToBuild -eq $null) {$itemToBuild = ls *.csproj | where {$_.Name -notmatch ".*\.v\d\.\d\.csproj"}}
Write-Host "Building Project ($($itemToBuild.Name))" -ForegroundColor Green
Exec { msbuild $itemToBuild  /t:Rebuild /p:"TargetFrameworkVersion=$version;Configuration=$configuration" /v:quiet /p:OutDir=$outdir/$version}
pop-location
}

Task Build-Project-Net40 {
$configuration = $script:hash.build_mode
$version ="v4.0"
$folder = ".\src"
push-location $folder
$itemToBuild = ls -Filter *.csproj | where {$_.Name -match ".*\."+$version+".csproj"}
if ($itemToBuild -eq $null) {$itemToBuild = ls *.csproj | where {$_.Name -notmatch ".*\.v\d\.\d\.csproj"}}
Write-Host "Building Project ($($itemToBuild.Name))" -ForegroundColor Green
Exec { msbuild $itemToBuild  /t:Rebuild /p:"TargetFrameworkVersion=$version;Configuration=$configuration" /v:quiet /p:OutDir=$outdir/$version}
pop-location
}

Task Build-Test-Project-Net45  { 
$version ="v4.5"
$folder = ".\tests"
$configuration = $script:hash.build_mode
push-location $folder

$itemToBuild = ls *.csproj | where {$_.Name -match ".*\."+$version+".csproj"}
if ($itemToBuild -eq $null) {$itemToBuild = ls *.csproj | where {$_.Name -notmatch ".*\.v\d\.\d\.csproj"}}
Write-Host "Building Project ($($itemToBuild.Name))" -ForegroundColor Green
Exec { msbuild $itemToBuild  /t:Rebuild /p:"TargetFrameworkVersion=$version;Configuration=$configuration" /v:quiet /p:OutDir=$outdir/$version}
pop-location
}

Task Test-Project -depends Test-Project-Net45 {
}

Task Test-Project-Net45 -depends Build-Test-Project-Net45 {
$version = "v4.5"
$configuration = $script:hash.build_mode
$gallio = (ls ".\tests\packages\GallioBundle*\bin\Gallio.Echo.exe").FullName
$ServiceTestDll = "Redlock.CSharp.Tests.dll"
#Add-PSSnapIn Gallio
#Run-Gallio "Staging\$($version)\$($ServiceTestDll)" -Filter "exclude Category:database" -rd "Staging\$($version)\reports" -rt html -ReportNameFormat "test-report"
&$gallio "Staging\$($version)\$($ServiceTestDll)" /f:"exclude Category:database" "/rd:Staging\$($version)\reports" /rt:html /rnf:"test-report"

Write-Host "UnitTest Report Generated in Staging\$($version)\reports\test-report.html" -ForegroundColor Green

}


Task Create-NugetPackage  -depends Build-Project-Net45, Build-Project-Net40 { 
if (test-path nuget) {rm -force -recurse nuget}
mkdir nuget
$nuget = (ls ".\tests\packages\NuGet.CommandLine*\tools\nuget.exe").FullName
&$nuget pack redlock-cs.nuspec -outputdirectory nuget
}

Task Publish-NugetPackage  -depends Create-NugetPackage { 
$apikey = cat apikey.txt
$nuget = (ls ".\tests\packages\NuGet.CommandLine*\tools\nuget.exe").FullName
$packagetopublish = ls *.nupkg
&$nuget push $packagetopublish -apikey $apikey
}

Task Build-Solution  -depends setConfiguration-Release, Build-Project { 
}
