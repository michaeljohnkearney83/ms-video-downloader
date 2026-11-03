Build Docker images from root:

PS C:\Users\{adUser}\Desktop\ms.vd> docker compose -f docker-compose.Dev.yaml up --build -d

Build for Release (Hidden):

PS C:\Users\{adUser}\Desktop\MS.VD\MS.Vid-dldr.Distributor> dotnet publish --configuration Release -p:OutputType=WinExe
