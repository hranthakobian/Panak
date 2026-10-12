$baseDir = "c:\Users\hrant\Documents\GitHub\Panak"
$staging = Join-Path $baseDir "staging_zip"
$zipPath = Join-Path $baseDir "Panak-Installer-v1.7.3.zip"
$publishZip = Join-Path $baseDir "publish\Panak-Installer-v1.7.3.zip"

if (Test-Path $staging) {
    Remove-Item -Recurse -Force $staging
}
New-Item -ItemType Directory -Path $staging | Out-Null

Copy-Item (Join-Path $baseDir "publish\Setup.exe") -Destination $staging

$readmeContent = @"
==================================================
                 PANAK v1.7.3
       Material You Popup Folders for Windows
==================================================

[Տեղադրում / Installation]
1. Գործարկեք `Setup.exe`-ն (Run Setup.exe)
2. Ընտրեք Ձեր նախընտրած կարգավորումները (Auto-start, Shortcuts)
3. Սեղմեք «Տեղադրել» (Install)

[Հիմնական հնարավորություններ / Features]
• Ժամանակակից Material 3 Expressive ոճի թղթապանակներ Windows-ի աշխատասեղանին
• Մկնիկի աջ կոճակի կրկնակի սեղմումով (Right double-click) արագ բացում
• Ֆայլերի միավորում թղթապանակի մեջ (Combine files on drag-drop)
• Dynamic 2x2 preview icon engine
• Windows-ի մեկնարկի ավտոմատ գործարկում (Auto-Start)
==================================================
"@

Set-Content -Path (Join-Path $staging "README.txt") -Value $readmeContent -Encoding UTF8

if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}
Compress-Archive -Path "$staging\*" -DestinationPath $zipPath -Force

Copy-Item $zipPath -Destination $publishZip -Force

Remove-Item -Recurse -Force $staging

Write-Host "Success! Created:" $zipPath "Size:" (Get-Item $zipPath).Length "bytes"
