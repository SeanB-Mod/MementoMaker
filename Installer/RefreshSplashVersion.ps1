param(
    [string]$IssPath = (Join-Path $PSScriptRoot 'MementoMaker.iss'),
    [string]$ImagePath = (Join-Path $PSScriptRoot 'Assets\WizardLarge.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $IssPath)) {
    throw "Installer script not found: $IssPath"
}
if (-not (Test-Path -LiteralPath $ImagePath)) {
    throw "Installer splash image not found: $ImagePath"
}

$iss = Get-Content -LiteralPath $IssPath -Raw
$match = [regex]::Match($iss, '#define\s+MyAppDisplayVersion\s+"([^"]+)"')
if (-not $match.Success) {
    throw 'Could not read MyAppDisplayVersion from MementoMaker.iss.'
}
$displayVersion = $match.Groups[1].Value.ToUpperInvariant()

$image = [System.Drawing.Bitmap]::FromFile($ImagePath)
try {
    $g = [System.Drawing.Graphics]::FromImage($image)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        # Version badge in WizardLarge.png. Redraw the whole badge so a shorter or longer
        # future version cannot leave pixels from the previous version behind.
        $x = 178; $y = 375; $w = 300; $h = 92; $radius = 34
        $fill = [System.Drawing.Color]::FromArgb(250, 181, 17)
        $border = [System.Drawing.Color]::FromArgb(210, 142, 0)
        $textColor = [System.Drawing.Color]::FromArgb(0, 50, 83)

        $path = New-Object -TypeName System.Drawing.Drawing2D.GraphicsPath
        try {
            $d = $radius * 2
            $path.AddArc($x, $y, $d, $d, 180, 90)
            $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
            $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
            $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
            $path.CloseFigure()

            $brush = New-Object -TypeName System.Drawing.SolidBrush -ArgumentList $fill
            $pen = New-Object -TypeName System.Drawing.Pen -ArgumentList $border, 3
            try {
                $g.FillPath($brush, $path)
                $g.DrawPath($pen, $path)
            }
            finally {
                $brush.Dispose()
                $pen.Dispose()
            }
        }
        finally {
            $path.Dispose()
        }

        $font = New-Object -TypeName System.Drawing.Font -ArgumentList 'Arial', 30.5, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Point)
        $textBrush = New-Object -TypeName System.Drawing.SolidBrush -ArgumentList $textColor
        $format = New-Object -TypeName System.Drawing.StringFormat
        try {
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center
            $rect = New-Object -TypeName System.Drawing.RectangleF -ArgumentList ([single]$x), ([single]($y - 1)), ([single]$w), ([single]$h)
            $g.DrawString($displayVersion, $font, $textBrush, $rect, $format)
        }
        finally {
            $font.Dispose()
            $textBrush.Dispose()
            $format.Dispose()
        }
    }
    finally {
        $g.Dispose()
    }

    # Save through a temporary file so GDI+ never tries to overwrite the file it opened.
    $temp = $ImagePath + '.tmp.png'
    $image.Save($temp, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $image.Dispose()
}
Move-Item -LiteralPath $temp -Destination $ImagePath -Force
Write-Host "Installer splash version refreshed to $displayVersion"
