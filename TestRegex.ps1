$text = "0.0108 - 0.0116 0.0019 0.0061 <0.00020 0.00044 92.0"
$match = [regex]::Match($text, "((?:[<]?\d+\.\d+\s*|[-]\s*|\+\s*)+)")
$match.Value

$vals = [regex]::Matches($match.Value, "[<]?\d+\.\d+")
foreach ($val in $vals) {
    Write-Host $val.Value
}
