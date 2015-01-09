Param(
	[String]$task = "Build-Project"
)

Invoke-WebRequest https://github.com/psake/psake/raw/master/psake.psm1 -OutFile psake.psm1
Import-Module .\psake.psm1

Invoke-Psake $task