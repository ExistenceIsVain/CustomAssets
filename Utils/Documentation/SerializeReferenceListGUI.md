# SerializeReferenceListGUI

`using Utils.EditorTools;` — сборка `Utils.Editor`, только редакторный код.

Инспектор для `[SerializeReference] List<База>` из наследников абстрактного класса. Вызывается
из своего `CustomEditor`. Даёт:

- сворачиваемый список со счётчиком и прокруткой после `maxHeight`;
- кнопку добавления с меню всех неабстрактных наследников;
- Expand All / Collapse All;
- у элемента — ▲▼, Copy, Paste под элемент и удаление;
- Paste в конец списка — копия хранится одна на весь редактор и переживает вход в Play Mode;
- кружок «включён/выключен» и скрытие элементов по условию.

Элементы с удалённым или переименованным классом (пустая ссылка) вычищаются сами.

```csharp
[CustomEditor(typeof(Cutscene))]
public class CutsceneEditor : Editor
{
    private SerializedProperty steps;

    private void OnEnable() => steps = serializedObject.FindProperty("steps");

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializeReferenceListGUI.Draw(
            steps,
            typeof(CutsceneStep),
            "Steps",
            elementLabel: value => ((CutsceneStep)value).Describe(),
            elementEnabled: value => ((CutsceneStep)value).enabled,
            addButtonLabel: "Add Step");

        serializedObject.ApplyModifiedProperties();
    }
}
```

## Параметры Draw

| Параметр | По умолчанию | Что задаёт |
|---|---|---|
| `list` | — | свойство `[SerializeReference]`-списка |
| `baseType` | — | база: в меню добавления — её неабстрактные наследники |
| `title` | — | заголовок, по нему список сворачивается |
| `readableName` | `DefaultName` | имя типа в меню и заголовке элемента |
| `headerButtons` | — | свои кнопки в заголовке элемента, до стандартных |
| `elementLabel` | — | подпись после имени типа, например краткое содержание |
| `maxHeight` | 450 | высота, после которой список прокручивается |
| `elementEnabled` | — | кружок: зелёный — включён, красный — выключен |
| `elementVisible` | — | скрыть элементы; стрелки переставляют среди видимых |
| `addButtonLabel` | `"Add"` | подпись кнопки добавления |

`DefaultName(type)` разбивает CamelCase по словам и убирает суффикс `Drawer`.
`SplitWords(name)` — только разбивка по словам.

Отличие от `[SubclassDrawer]`: тот рисует одно поле или каждый элемент массива отдельно и
работает без своего `CustomEditor`; этот рисует весь список целиком с действиями над элементами.

Пример — сэмпл **Utils Samples**, папка `SerializeReferenceList`, префаб `SerializeReferenceListExample`: `ExampleGO` — катсцена из шагов, которая проигрывается по Play,
`ExampleClass` — база шагов, `Editor/ExampleGOEditor` — инспектор.
