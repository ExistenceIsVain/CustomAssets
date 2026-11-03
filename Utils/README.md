# Utils

Общий набор утилит для Unity-проектов. Unity 6000.0+.

## Подключение

В `Packages/manifest.json` проекта:

```json
"com.existenceisvain.utils": "https://github.com/ExistenceIsVain/CustomAssets.git?path=/Utils"
```

Конкретная версия — тег в конце ссылки: `...CustomAssets.git?path=/Utils#v1.0.0`.

Код игры в своём asmdef должен ссылаться на сборку `Utils`. Без asmdef (Assembly-CSharp)
ссылка не нужна. Редакторная часть `Utils.Editor` подключается сама.

## Модули

| Модуль | Namespace | Что делает | Документация | Папка сэмпла |
|---|---|---|---|---|
| Атрибуты инспектора | `Utils` | `[EditorReadOnly]`, `[MinMaxRange]`, `[SteppedRange]`, `[StringConstant]` + `[Region]`, `[SubclassDrawer]`, `[SOGroup]` + `[SOSubGroup]` | [Attributes.md](Documentation/Attributes.md) | `Attributes` |
| LayerMaskExtensions | `Utils` | `mask.Contains(gameObject)`, `mask.Contains(component)`, `mask.ToSingleLayer()` | [LayerMaskExtensions.md](Documentation/LayerMaskExtensions.md) | `LayerMasks` |
| FastCooldown | `Utils.Cooldowns` | кулдауны по игровому времени: атака, дэш, антиспам UI | [FastCooldown.md](Documentation/FastCooldown.md) | `FastCooldowns` |
| GlobalCooldown | `Utils.Cooldowns` | кулдауны по настенным часам с сохранением: дейли, энергия, бустеры | [GlobalCooldown.md](Documentation/GlobalCooldown.md) | `GlobalCooldowns` |
| GameplayTick | `Utils.Tick` | тик-каналы с фиксированным шагом: DoT, реген, «раз в секунду» | [GameplayTick.md](Documentation/GameplayTick.md) | `Tick` |
| Transmitter | `Utils.Messaging` | синхронная шина: события `IReceiver<T>` и запросы `IProvider<TReq, TResp>` + окно монитора | [Transmitter.md](Documentation/Transmitter.md) | `Messaging` |
| GlobalAsync | `Utils.Async` | задачи на `Awaitable` с владельцем, тегом и отменой | [GlobalAsync.md](Documentation/GlobalAsync.md) | `Async` |
| GizmosObject | `Utils.Gizmo` | разметка гизмо без своего скрипта: сфера, куб, дуга, стрелка, меш, коллайдер, путь, точки, текст | [Gizmo.md](Documentation/Gizmo.md) | `Gizmo` |
| SerializeReferenceListGUI | `Utils.EditorTools` | инспектор `[SerializeReference]`-списка наследников | [SerializeReferenceListGUI.md](Documentation/SerializeReferenceListGUI.md) | `SerializeReferenceList` |
| Окна редактора | `Utils.EditorTools` | `Tools > Scriptable Object Editor`, `Tools > Transmitter Monitor`, `Tools > Lorem Ipsum Generator` | [EditorWindows.md](Documentation/EditorWindows.md) | — |

## Сэмплы

`Window > Package Manager` → Utils → вкладка **Samples** → **Utils Samples** → **Import**.
Сэмпл копируется в `Assets/Samples/Utils/<версия>/Utils Samples/` и компилируется вместе с проектом.

Открой `UtilsDemo.unity` и нажми Play: сверху вкладка на каждый модуль, камера подлетает к
выбранному примеру, на панели — его кнопки и состояние, в редакторе пример выделяется, и его
поля видны в инспекторе.

| Папка | Префаб | Что показывает |
|---|---|---|
| `Attributes` | `AttributesExample` | все атрибуты на одном компоненте, `ExampleConfig` с `[SOGroup]` и `[SOSubGroup]` |
| `LayerMasks` | `LayerMaskExample` | зона-триггер по маске, цели в радиусе, шары со слоем из `ToSingleLayer` |
| `FastCooldowns` | `FastCooldownExample` | меч опускается при ударе и поднимается по мере перезарядки, рывок по реальному времени |
| `GlobalCooldowns` | `GlobalCooldownExample` | сундук с ежедневной наградой и энергия, переживают перезапуск |
| `Tick` | `GameplayTickExample` | яд на канале Gameplay, регенерация на своём канале, полоска HP |
| `Messaging` | `TransmitterExample` | событие, запрос и запрос без провайдера с `silent` |
| `Async` | `GlobalAsyncExample` | теги Restart/Skip, Delay, WaitUntil, фоновый поток, отмена |
| `Gizmo` | `GizmoExample` | настроенная разметка врага и свой отрисовщик |
| `SerializeReferenceList` | `SerializeReferenceListExample` | катсцена из шагов со своим инспектором, проигрывается по Play |
| `SOEditor` | — | конфиги оружия и зелий для `Tools > Scriptable Object Editor`: группа Weapon с сабгруппами Enemy, Player, Boss и Ungrouped / Consumables |

В каждой папке — `ExampleClass` (обычный C#-класс: сервис, данные, база иерархии) и
`ExampleGO` (компонент префаба), в своём namespace `Utils.Samples.<Модуль>`.
Префаб можно перетащить в свою сцену: кнопки панели появятся, если рядом лежит префаб
`Common/DemoMenu`; без него те же действия — в контекстном меню компонента (ПКМ по заголовку).
Ввод с клавиатуры не используется: сэмплы не зависят от Input System или старого Input Manager.

Материалы сэмплов сделаны на шейдере URP Lit. В проекте на Built-in они будут розовыми —
поменяй шейдер на Standard.

## Атрибуты

```csharp
using Utils;

[EditorReadOnly] public int spawnedCount;           // видно, но не правится
[MinMaxRange(0f, 10f)] public Vector2 delay;        // Vector2 или Vector2Int
[SteppedRange(0f, 100f, 5f)] public float chance;   // слайдер с шагом, float или int
[StringConstant(typeof(AnimatorParams))] public string trigger;  // выпадающий список const string
[SerializeReference, SubclassDrawer] public Effect effect;       // выбор наследника для [SerializeReference]
```

У поля работает только один PropertyDrawer: атрибуты-дроверы между собой не совмещаются.
Подробно о каждом — в [Attributes.md](Documentation/Attributes.md).

## Структура

```
Runtime/   сборка Utils — работает в игре
Editor/    сборка Utils.Editor — только редактор
Documentation/    подробные описания модулей
Samples~/         сэмпл Utils Samples: Package Manager > Samples > Import (Unity не компилирует папки с ~)
```

Изменения по версиям — [CHANGELOG.md](CHANGELOG.md).
