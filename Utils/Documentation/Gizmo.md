# GizmosObject

`using Utils.Gizmo;` — сборка `Utils`, инспектор — `Utils.Editor`.

Разметка гизмо без своего скрипта: повесь `GizmosObject` (Add Component → Utils → Gizmos Object)
и добавь элементы кнопкой **Add Drawer**. Удобно для левел-дизайна: зоны триггеров, радиусы,
маршруты, подписи прямо в сцене.

## Отрисовщики

| Тип | Что рисует |
|---|---|
| Sphere | сфера: свой радиус, по масштабу объекта или сумма |
| Cube | куб: свой размер, по масштабу или сумма; `rotateToObject` — по повороту объекта |
| Arc | сектор круга: зона обзора, конус атаки |
| Arrow | стрелка направления относительно объекта или по своим углам |
| Mesh | меш из `MeshFilter` объекта |
| Collider | повторяет `BoxCollider` или `SphereCollider` объекта |
| Path | линия через трансформы сцены |
| Points | список точек; пока объект выделен — каждую можно тащить хэндлом |
| Text | подпись в сцене |

У всех: `enabled` и `disableOnSelect` (не рисовать, пока объект выделен — чтобы не закрывать хэндлы).
У фигур: `drawStyle` (заливка, контур или оба), `shapeColor`, `wireColor`, `offset`.

В заголовке элемента — кружок включён/выключен, кнопки перестановки, Copy/Paste (между
объектами тоже) и **Reset** — сброс к значениям по умолчанию.

Если у объекта нет нужного компонента (Mesh, Collider), вместо фигуры в сцене появляется
красная подпись — в консоль ничего не пишется.

## Свой отрисовщик

```csharp
[Serializable]
public class CrossGizmoDrawer : ShapeGizmoDrawer   // или GizmosDrawer, если стиль и цвета не нужны
{
    public float size = 0.5f;

#if UNITY_EDITOR
    public override void Draw(Transform transform)
    {
        Gizmos.color = wireColor;
        Gizmos.DrawLine(transform.position - Vector3.right * size, transform.position + Vector3.right * size);
    }

    // Необязательно: хэндлы в сцене, пока объект выделен.
    public override void OnSceneGUI(GizmosObject owner) { }
#endif
}
```

Класс сам появится в меню Add Drawer: имя без суффикса `GizmoDrawer`, разбитое по словам.
Поля объявляй вне `#if UNITY_EDITOR` — иначе раскладка данных в сборке игры разойдётся со сценой.

## Заполнение из кода

```csharp
GizmosObject gizmos = GetComponent<GizmosObject>();
gizmos.GetDrawers().Add(new SphereGizmoDrawer { radius = 6f, drawStyle = GizmosDrawer.DrawStyle.OnlyWire });
```

Список правится в обход SerializedObject, поэтому в редакторе после изменения нужен
`EditorUtility.SetDirty(gizmos)`.

## Перенос со старой версии

Старые `gizmoSphereDrawer` и другие классы из `Helpers.Gizmo` помечены `[MovedFrom]`, их поля —
`[FormerlySerializedAs]`: уже настроенная разметка в сценах подхватывается при открытии сцены.

Пример — сэмпл **Utils Samples**, папка `Gizmo`, префаб `GizmoExample` (разметка уже настроена): `ExampleGO` заполняет разметку врага из кода в `Reset`,
`ExampleClass` — свой отрисовщик.
