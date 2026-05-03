Простой лаунчер для Roblox: вводите Place ID (только цифры) или полный HTTPS URL игры; приложение откроет страницу игры в браузере — затем установленный Roblox Player должен запуститься.

Как получить .exe через GitHub Actions (без установки на ваш ПК):

Создайте новый публичный репозиторий на GitHub (например: roblox-launcher).
В корне репозитория добавьте файлы: Program.cs, RobloxLauncher.csproj, .github/workflows/build.yml, .gitignore, README.md. (через Add file → Create new file или загрузкой ZIP).
Сделайте коммит в ветку main.
Перейдите в Actions → откройте workflow "Build Windows x64 single-file". Если GitHub попросит разрешить first‑time workflow, нажмите разрешить.
Подождите завершения (несколько минут). В успешном запуске справа появится секция Artifacts → скачайте RobloxLauncher-windows-x64.
Распакуйте ZIP, внутри будет RobloxLauncher.exe — запустите его.
Замечания:

Workflow создаёт self-contained single-file exe (runtime включён) — значит .NET на ПК не нужен, но файл будет достаточно крупный (~60–120 MB).
Windows может показать предупреждение SmartScreen для неподписанных приложений — это нормально.
Приложение не хранит логины/пароли и не использует приватные API — оно просто открывает официальный URL игры.
