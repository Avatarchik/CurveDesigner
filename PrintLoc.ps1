Get-ChildItem -Path .\Assets -Recurse -Include *.cs | Get-Content | Measure-Object -Line
