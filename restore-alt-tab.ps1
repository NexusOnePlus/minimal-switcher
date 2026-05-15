# script para restaurar el alt+tab nativo de windows si algo sale mal
$path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer"
$name = "AltTabSettings"

if (Test-Path $path) {
    $val = Get-ItemProperty -Path $path -Name $name -ErrorAction SilentlyContinue
    if ($val) {
        Write-Host "Quitando modificacion de AltTabSettings..."
        Remove-ItemProperty -Path $path -Name $name
        Write-Host "Listo. Reinicia el Explorador de archivos si no ves el cambio inmediato."
    } else {
        Write-Host "No se encontro la modificacion en el registro."
    }
} else {
    Write-Host "La ruta del registro no existe."
}

# opcional: reiniciar explorer.exe
# stop-process -name explorer -force
