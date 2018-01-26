
Read-Host -Prompt 'This script will setup the initial data directories for Hybrasyl. Press any key to continue.'

$mydocuments = [environment]::getfolderpath("mydocuments")

New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\mapfiles
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\castable
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\item
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\item
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\npc
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\reactor
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\scripts\startup
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\castables
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\creatures
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\items
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\itemvariants
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\localization
New-Item -Force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\lootsets
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\maps
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\nations
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\npcs
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\spawngroups
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\statuses
New-Item -force -ItemType directory -Path $mydocuments\Hybrasyl\world\xml\worldmaps

