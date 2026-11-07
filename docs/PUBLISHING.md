# Публикация на GitHub

## Репозиторий

Рекомендуемое имя: **Horizon-offine-helper**.

Описание для поля Description:

> Windows helper that prepares and launches Horizon using existing Minecraft 26.3 files from other launchers. English and Russian UI. MIT licensed.

Темы: `horizon`, `minecraft`, `launcher`, `windows`, `winforms`, `csharp`.

В локальном репозитории уже есть лицензия MIT и файлы проекта. Добавлять ещё одну лицензию через интерфейс GitHub не нужно.

1. В GitHub Desktop выберите **File → Add local repository** и папку этого проекта, если она ещё не добавлена.
2. Убедитесь, что выбран **Horizon-offine-helper**, затем нажмите **Publish repository**.
3. Для открытого проекта снимите **Keep this code private**, проверьте имя и описание и подтвердите публикацию.
4. Откройте репозиторий на GitHub и проверьте результат задачи **Windows build** во вкладке Actions.

[Официальная инструкция GitHub Desktop по добавлению локального репозитория](https://docs.github.com/en/desktop/adding-and-cloning-repositories/adding-a-repository-from-your-local-computer-to-github-desktop).

## Релиз 1.1.0

Сборка:

```powershell
.\package.ps1
```

На странице GitHub откройте **Releases → Draft a new release**. Укажите тег **v1.1.0**, целевую ветку **main**, название **Horizon-offine-helper 1.1.0**. Для описания используйте [RELEASE_NOTES.md](RELEASE_NOTES.md).

Прикрепите только эти файлы из `dist`:

- `Horizon-offine-helper-1.1.0-windows.zip`;
- `SHA256SUMS.txt`.

Исходный код GitHub добавит к релизу автоматически. Сборки Horizon, Minecraft, Java, ресурсы игры и локальные журналы прикреплять не требуется. Проверьте описание и файлы, затем опубликуйте релиз.

## Следующие версии

Обновите `VERSION`, `AssemblyVersion` и `AssemblyFileVersion` в `src/Program.cs`, а также версию в `src/app.manifest`. Добавьте запись в `CHANGELOG.md`, выполните сборку с проверками и отдельно проверьте реальную подготовку и запуск игры. При изменении поддерживаемой версии Minecraft обновите описание и документацию.

GitHub Actions собирает и проверяет проект, но не публикует релизы автоматически.
