
# Configurables
$JAVA_EXE = "C:\Program Files (x86)\Java\jre7\bin\java.exe"

$DIR_DEST = ".\XSD"

function CleanDestinationDirectory
{
    if (Test-Path -path "$DIR_DEST")
    {
	Write-Host "Removing $DIR_DEST and all contents..."
        Remove-Item -Recurse -Force "$DIR_DEST"
    }

    if (!(Test-Path -path "$DIR_DEST"))
    {
	Write-Host "Creating $DIR_DEST directory..."
        New-Item -ItemType directory -Path "$DIR_DEST" | Out-Null
    }
}


function BuildSchemaArgs($baseName)
{
    return "RelaxNG\$baseName.rnc", "$DIR_DEST\$baseName.xsd"
}


CleanDestinationDirectory

$trangArgs = "-jar", "trang-20091111\trang.jar"

# Generate various schemas
Write-Host "Building Quest Schema..."
$schemaArgs = BuildSchemaArgs("HBSchemas-All")
Invoke-Command -ScriptBlock { & $JAVA_EXE $trangArgs $schemaArgs }


# Allow user to examine results
Write-Host "Press any key to exit..."
$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
