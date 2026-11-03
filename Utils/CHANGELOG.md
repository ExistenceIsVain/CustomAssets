# Changelog

## [1.1.0]

### Добавлено

- `[SOSubGroup("Имя")]` — второй уровень групп в `Tools > Scriptable Object Editor`. Сабгруппа
  наследуется и переопределяется наследником; без `[SOGroup]` попадает в «Ungrouped».
- Сэмпл: папка `SOEditor` — классы конфигов с группами и сабгруппами и готовые ассеты для окна.

### Изменено

- Группу задаёт самый базовый класс с `[SOGroup]`: другое имя на наследнике игнорируется с
  предупреждением в консоли (раньше побеждал наследник).
- Группа «Ungrouped» показывается последней; в группе сначала сабгруппы, потом ассеты.

## [1.0.0]

Первая версия пакета. Раньше набор копировался папкой `Assets/Utils` и компилировался в
Assembly-CSharp, поэтому из кода в своём asmdef был недоступен.

### Перенос проекта на пакет

1. Удалить старую папку `Assets/Utils`, иначе типы будут объявлены дважды.
2. Добавить пакет в `Packages/manifest.json` (см. README).
3. В asmdef игры добавить ссылку на `Utils`.
4. Добавить `using` по таблице ниже.

| Было | Стало |
|---|---|
| глобальный namespace (атрибуты, кулдауны, Transmitter) | `Utils`, `Utils.Cooldowns`, `Utils.Messaging` |
| `Helpers.Gizmo` | `Utils.Gizmo` |
| `Helpers.SerializeReferenceListGUI` | `Utils.EditorTools.SerializeReferenceListGUI` |
| `Core.Systems.Tick` | `Utils.Tick` |
| `Project.Scripts.Editor` (SO Editor) | `Utils.EditorTools` |
| `Helpers.LayerHelper`, `Helpers.Layers` | `Utils.LayerMaskExtensions` |
| `GlobalCoroutine`, `CoroutineHandle` | `Utils.Async.GlobalAsync`, `AsyncHandle` — см. GlobalAsync.md |

Компонент `GizmosObject` сохранил GUID скрипта, а отрисовщики помечены `[MovedFrom]`
и `[FormerlySerializedAs]` — уже настроенные гизмо в сценах переносятся без потерь.

### Добавлено

- Сэмпл **Utils Samples** (`Samples~`, импорт из Package Manager): демо-сцена `UtilsDemo` со
  всеми модулями и меню по вкладкам, готовый префаб на каждый модуль. В каждой папке модуля —
  `ExampleClass` и `ExampleGO` в своём namespace `Utils.Samples.<Модуль>`.
- Документация для модулей, у которых её не было: Attributes, LayerMaskExtensions, GameplayTick,
  Gizmo, SerializeReferenceListGUI, окна редактора.
  Вся документация — в папке `Documentation` пакета: видна в окне Project рядом с кодом.
- `.meta` у файлов сэмпла: после импорта GUID скриптов, префабов и материалов одинаковые во всех проектах.
- `LayerMaskExtensions`: `Contains(GameObject)`, `Contains(Component)`, `Contains(int)`, `ToSingleLayer()`.

### Убрано

- `GlobalInstantiate` — обёртка над `Instantiate` без пула; терялся учёт, когда спавнился компонент.
- `GlobalCoroutine` — заменён на `GlobalAsync` поверх `Awaitable`.
- `TransmitterHistory` — пустой шаблон.
- `LayerHelper` / `Layers` — заменены на `LayerMaskExtensions`. `Layers.ConvertLayer` через
  `Mathf.Log` давал неверный слой для 31-го слоя и для масок из нескольких слоёв.

### Исправлено и изменено

- **Transmitter**
  - Записи монитора ведутся только в редакторе и не растут: повторная регистрация того же
    объекта оживляет его запись, мёртвых записей на канал — не больше 32.
  - `silent: true` теперь убирает сообщение об отсутствии провайдера, а не понижает его до предупреждения.
    `silent` добавлен в `Request` с callback и в `Ask`.
  - Провайдеры ищутся по паре `TRequest` + `TResponse`: два провайдера с одним запросом и
    разными ответами больше не дают `InvalidCastException`.
  - `Send` не падает, если получатель во время обработки снимает с регистрации других.
- **Gizmo**
  - Отрисовщики переименованы: `gizmoArcDrawer` → `ArcGizmoDrawer` и т.д.; поля — в camelCase.
  - Общая база `ShapeGizmoDrawer` / `ScaledShapeGizmoDrawer` вместо копипасты в каждом классе.
  - Дуга: контур рисуется вокруг той же оси, что и заливка.
  - Коллайдер и меш без нужного компонента пишут подпись в сцене, а не ошибку в консоль каждый кадр.
    Коллайдер понимает и `SphereCollider`.
  - Поля сериализуются и в сборке игры: раньше они были под `#if UNITY_EDITOR`, и раскладка
    данных расходилась со сценой.
  - Пустой элемент списка (удалённый класс) больше не роняет отрисовку.
  - Кнопка Reset создаёт новый экземпляр отрисовщика; методы `ResetDrawer` убраны.
    `OnSceneGUI` получает `GizmosObject`, а не `Transform`.
- **SerializeReferenceListGUI** — подпись кнопки добавления задаётся параметром `addButtonLabel`.
- **SO Editor**
  - Папка по умолчанию — `Assets`.
  - Вкладки хранятся в `UserSettings/ScriptableObjectEditor`, а не в `persistentDataPath` игры.
  - Ассеты ищутся только в выбранной папке, а не загружаются со всего проекта.
- **SteppedRange** — проверка после компиляции видит поля во всех сборках, включая asmdef проекта.
- **MinMaxRange** — поддержка `Vector2Int`, `BeginProperty` (жирный шрифт для переопределений префаба, меню по правому клику), корректный отступ.
- **StringConstant** — `BeginProperty`, корректный отступ, выбор из меню не теряет свойство.
- **GameplayTick** — `GetChannel`, `RemoveChannel` и сеттеры каналов не создают объект тика при выходе из Play Mode.
- **LoremIpsum** — текст случайный; превью стабильно до кнопки Shuffle, у каждого текстового компонента свой текст.
- **SubclassDrawer** — файл атрибута назван по классу: `SubclassDrawerAttribute.cs`.
