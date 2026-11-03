# Атрибуты инспектора

`using Utils;` — сборка `Utils`, дроверы — в `Utils.Editor`.

У поля работает только один PropertyDrawer: атрибуты из этого списка между собой и с `[Range]`
не совмещаются — Unity не гарантирует, какой победит. Декораторы (`[Header]`, `[Tooltip]`,
`[Space]`) совмещаются с любым.

Пример — сэмпл **Utils Samples**, папка `Attributes`, префаб `AttributesExample`: `ExampleGO` со всеми атрибутами на полях, `ExampleClass` —
иерархия для `[SubclassDrawer]` и константы для `[StringConstant]`, `ExampleSO` — `[SOGroup]` и `[SOSubGroup]`.

## EditorReadOnly

Поле видно в инспекторе, но не правится. Это подсказка «не трогай», а не защита: значение
меняется кодом, в режиме Debug инспектора, через Preset и копипаст.

```csharp
[EditorReadOnly] [SerializeField] private int spawnedCount;
```

На массиве действует на элементы — размер и кнопки +/- остаются активными.

## MinMaxRange

Диапазон «от и до» одним слайдером с полями по краям. `Vector2` или `Vector2Int`:
`x` — минимум, `y` — максимум. Минимум не может стать больше максимума.

```csharp
[MinMaxRange(0f, 5f)] public Vector2 attackDelay = new Vector2(0.5f, 1.5f);
[MinMaxRange(1, 20)] public Vector2Int goldDrop = new Vector2Int(3, 8);

float delay = Random.Range(attackDelay.x, attackDelay.y);
```

## SteppedRange

Слайдер с шагом, как `[Range]`, но значение прилипает к кратным шагу. `float` или `int`.

```csharp
[SteppedRange(0f, 100f, 5f)] public float critChance;   // 0, 5, 10 … 100
[SteppedRange(3f, 100f, 5f)] public float speed;        // 3, 8, 13 … и сам 100
```

- Ступени отсчитываются от `min`, а не от нуля.
- `max` достижим всегда, даже если шаг не делит диапазон нацело.
- Шаг 0 или меньше — обычный `Range` без прилипания.
- Округляется только при изменении в инспекторе: значение из кода или старого ассета не переписывается молча.

Неверные аргументы (min > max, шаг больше диапазона, NaN) не бросают исключение: ошибка
показывается плашкой над полем и пишется в консоль после каждой компиляции — с именем
класса и поля.

## StringConstant и Region

Выпадающий список вместо ручного ввода строки — для имён параметров аниматора, тегов,
ключей звуков. Источник — класс с `public const string`.

```csharp
public static class AnimatorParams
{
    [Region("Triggers")]
    public const string Attack = "Attack";
    public const string Dodge = "Dodge";      // тоже в Triggers — до следующего [Region]

    [Region("Bools")]
    public const string IsBlocking = "IsBlocking";

    public static class Sounds                // вложенный класс — своё подменю «Sounds»
    {
        public const string Hit = "sfx_hit";
    }
}

[StringConstant(typeof(AnimatorParams))] public string trigger = AnimatorParams.Attack;
```

Константы без `[Region]` попадают в корень меню. Если в поле записано значение, которого нет
среди констант, оно показывается как есть.

## SubclassDrawer

Выбор конкретного наследника для поля `[SerializeReference]`: в инспекторе — выпадающий список
типов, под ним — поля выбранного. Работает и для массивов и `List<T>` — у каждого элемента свой список.

```csharp
[Serializable] public abstract class Effect { }
[Serializable] public class Heal : Effect { public int amount; }
[Serializable] public class Burn : Effect { public float duration; }

[SerializeReference, SubclassDrawer] public Effect effect;
[SerializeReference, SubclassDrawer] public List<Effect> effects;
[SerializeReference, SubclassDrawer(typeof(Effect))] public object anyEffect;  // база явно
```

- В список попадают неабстрактные `[Serializable]` наследники с конструктором без параметров.
- Наследники из другого namespace группируются в подменю по namespace.
- После «+» или Duplicate элементы, указывающие на один экземпляр, получают свои копии.
- Смена типа поддерживает Undo и мультивыделение.

## SOGroup и SOSubGroup

Группа и сабгруппа класса ScriptableObject в окне `Tools > Scriptable Object Editor` — два уровня,
как папки в окне Project.

```csharp
[SOGroup("Weapon")]
public abstract class WeaponConfig : ScriptableObject { }

[SOSubGroup("Enemy")]  public class EnemyWeaponConfig : WeaponConfig { }   // Weapon / Enemy
[SOSubGroup("Player")] public class PlayerWeaponConfig : WeaponConfig { }  // Weapon / Player
[SOSubGroup("Boss")]   public class BossWeaponConfig : EnemyWeaponConfig { } // Weapon / Boss
```

- **Группа задаётся один раз** — самым базовым классом с `[SOGroup]`. Наследник её не меняет:
  `[SOGroup]` с другим именем на наследнике игнорируется, в консоль — предупреждение.
- **Сабгруппа наследуется и переопределяется** — берётся у ближайшего класса с `[SOSubGroup]`.
- Без `[SOGroup]` ассет попадает в группу «Ungrouped», с `[SOSubGroup]` — в её сабгруппу.
- В группе сначала сабгруппы, потом ассеты без сабгруппы; «Ungrouped» — последней.

Пример — сэмпл **Utils Samples**, папка `SOEditor`: те же классы и готовые ассеты в `Configs`. Само окно описано в [EditorWindows.md](EditorWindows.md).
