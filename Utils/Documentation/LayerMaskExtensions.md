# LayerMaskExtensions

`using Utils;` — сборка `Utils`.

Проверка «входит ли слой объекта в маску» одной строкой вместо `(mask.value & (1 << go.layer)) != 0`.

```csharp
[SerializeField] private LayerMask targets;

private void OnTriggerEnter(Collider other)
{
    if (!targets.Contains(other))   // Component: коллайдер, трансформ, любой компонент
        return;
}

bool isTarget = targets.Contains(gameObject);
bool hasLayer = targets.Contains(8);   // номер слоя 0–31
```

| Метод | Что делает |
|---|---|
| `mask.Contains(GameObject)` | слой объекта входит в маску |
| `mask.Contains(Component)` | слой объекта компонента входит в маску |
| `mask.Contains(int layer)` | слой с номером 0–31 входит в маску; вне диапазона — `false` |
| `mask.ToSingleLayer()` | номер слоя, если в маске ровно один слой, иначе `-1` |

`ToSingleLayer` нужен, чтобы выставить `gameObject.layer` из поля `LayerMask` в инспекторе:

```csharp
[SerializeField] private LayerMask projectileLayer;

int layer = projectileLayer.ToSingleLayer();
if (layer >= 0)
    projectile.layer = layer;
```

Маску из имён собирай один раз и кешируй: `LayerMask.GetMask("Player", "Enemy")` ищет слои
по имени и при каждом вызове выделяет массив под `params`.

Пример — сэмпл **Utils Samples**, папка `LayerMasks`, префаб `LayerMaskExample`: `ExampleGO` — зона-триггер по маске и шары, которым слой задаётся
через `ToSingleLayer`, `ExampleClass` — отбор целей в радиусе.
