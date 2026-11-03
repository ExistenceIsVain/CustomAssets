# GameplayTick

`using Utils.Tick;` — сборка `Utils`.

Тик-каналы с фиксированным шагом: урон от яда раз в 0.5 с, регенерация раз в секунду,
«медленная» логика ИИ. Каждый канал копит время и вызывает подписчиков, когда набрался шаг.

Объект `GameplayTick` создаётся сам при первом обращении и живёт в `DontDestroyOnLoad`.
Его можно и положить в сцену заранее — чтобы настроить каналы в инспекторе.

## Каналы

| Канал | Шаг | Доступ |
|---|---|---|
| `Gameplay` | 0.5 с (`TickRate`) | `GameplayTick.OnTick` |
| `Second` | 1 с | `GameplayTick.OnSecond` |
| свой | любой | `Subscribe(id, handler, rate)` |

```csharp
private void OnEnable() => GameplayTick.OnTick += OnTick;
private void OnDisable() => GameplayTick.OnTick -= OnTick;

// Аргумент — длина шага в секундах: урон «в секунду» × шаг не зависит от TickRate.
private void OnTick(float tickDuration) => hp -= poisonPerSecond * tickDuration;

GameplayTick.Subscribe("Regen", OnRegen, rate: 1f);   // канал создаётся при первой подписке
GameplayTick.Unsubscribe("Regen", OnRegen);
```

Отписка (`-=`, `Unsubscribe`), `GetChannel`, `RemoveChannel` и сеттеры каналов не создают объект
тика — их безопасно звать из `OnDisable`/`OnDestroy`, в том числе при выходе из Play Mode.

## Управление

| Метод | Что делает |
|---|---|
| `Pause()` / `Resume()` | остановить и продолжить все каналы |
| `RestartTick()` | обнулить накопленное время всех каналов |
| `SetChannelPaused(id, paused)` | пауза одного канала |
| `SetChannelRate(id, rate)` | новый шаг канала, минимум 0.01 с |
| `ResetChannel(id)` | обнулить накопленное время канала |
| `GetChannel(id)` | канал или `null`, если его нет |
| `GetOrCreateChannel(id, rate, startPaused)` | канал, создать при отсутствии |
| `RemoveChannel(id)` | удалить канал и всех подписчиков |

У `TickChannel`: `Rate`, `IsPaused`, `UseUnscaledTime`, `Progress` (0–1 до следующего тика — для полоски UI).

Кадровые события: `OnFrame(scaledDelta)` и `OnUnscaledFrame(unscaledDelta)` — каждый кадр, пока тик не на паузе.

## Защита от рывков

- Кадр длиннее 0.25 с считается за 0.25 с.
- За кадр канал выдаёт не больше 8 тиков, остаток сбрасывается — после фриза нет лавины тиков.
- Каналы, добавленные или удалённые во время тика, применяются после его конца.

Пример — сэмпл **Utils Samples**, папка `Tick`, префаб `GameplayTickExample`: `ExampleGO` — яд на канале Gameplay, `ExampleClass` — регенерация
на своём канале с отпиской в `Dispose`.
