# Окна редактора

Сборка `Utils.Editor`. Все окна — в меню `Tools`. Код примеров не нужен: окна работают
с тем, что уже есть в проекте.

## Scriptable Object Editor

`Tools > Scriptable Object Editor` — все ScriptableObject выбранной папки слева, инспектор
выбранного справа. Удобно править конфиги баланса, не прыгая по Project.

- **Вкладки-папки** сверху: Add / Edit / Remove или ПКМ по вкладке. По умолчанию одна вкладка — `Assets`.
- **Группы и сабгруппы** — по атрибутам `[SOGroup("Имя")]` и `[SOSubGroup("Имя")]` на классе (см. [Attributes.md](Attributes.md)), без группы — «Ungrouped». Expand All / Collapse All раскрывают оба уровня.
- **Поиск** по имени ассета — список без групп.
- **Save** — записать ассет на диск, **Ping** — подсветить в Project, **Select** — выделить.
- **Refresh** — перечитать папку после создания или удаления ассетов.

Вкладки хранятся в `UserSettings/ScriptableObjectEditor/filter-path-tabs.json` — это личная
настройка, в git не попадает (стандартный Unity `.gitignore`). Кнопка **Open Settings Folder**
открывает эту папку.

Ищутся ассеты только внутри папки вкладки: путь должен существовать, например `Assets/Data`.

## Transmitter Monitor

`Tools > Transmitter Monitor` — живой список регистраций `Transmitter` в Play Mode:
получатели по типу сообщения и провайдеры по паре «запрос → ответ», класс и объект,
время регистрации, мёртвые ссылки. По клику — открыть скрипт или выделить объект.
Подробно — в [Transmitter.md](Transmitter.md).

## Lorem Ipsum Generator

`Tools > Lorem Ipsum Generator` — заполнить текстовые компоненты UI текстом-заглушкой,
чтобы проверить вёрстку.

1. Выдели объекты в Hierarchy. С **Include Children** берутся и все дочерние.
2. Задай **Word Count**. Превью показывает текст, который получит первый компонент.
3. **Shuffle** — другой текст, **Generate** — записать, **Clear** — очистить.

Понимает `TMP_Text` (TextMeshPro) и legacy `UI.Text`. У каждого компонента свой текст.
Всё через Undo.
