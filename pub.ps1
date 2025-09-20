cd Wires.Kni
dotnet publish

Copy-Item -Path ".\bin\Release\net8.0\publish\wwwroot\*" -Destination ".\..\..\WiresHosting\" -Recurse -Force

cd ..\..\WiresHosting\

git add .
git commit -m "pub"
git push