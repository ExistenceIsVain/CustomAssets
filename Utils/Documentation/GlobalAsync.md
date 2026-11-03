# GlobalAsync

`using Utils.Async;` — сборка `Utils` из пакета `com.existenceisvain.utils`.

Запускает асинхронные задачи на `Awaitable` с владельцем и тегом. Нужен там, где задачу
запускает не `MonoBehaviour` (сервис, контроллер, состояние машины состояний) или где задачи
нужно останавливать группой — все задачи владельца или одну по имени.

Заменяет старый `GlobalCoroutine`: корутины, скрытый объект `DontDestroyOnLoad` и обход
мёртвых владельцев больше не нужны — отмену дают токены Unity.

> Только главный поток. Продолжения `Awaitable` и так выполняются на нём.
> Нужен Unity 6000.0+ (`Awaitable`, `destroyCancellationToken`, `Application.exitCancellationToken`).

## Когда задача останавливается

Задача получает `CancellationToken`. Он отменяется:

- при `handle.Stop()`, `StopByTag(owner, tag)`, `StopAllFor(owner)` или `StopAll()`;
- при уничтожении владельца, если он `MonoBehaviour` (`destroyCancellationToken`);
- при выходе из Play Mode или игры (`Application.exitCancellationToken`).

Токен нужно передавать во **все** ожидания внутри задачи — иначе отмена дождётся конца
текущего ожидания. `OperationCanceledException` от отмены глотается, остальные исключения
пишутся в Console с владельцем в качестве контекста.

Владелец, который не `MonoBehaviour`, держится в реестре, пока его задачи не закончатся.
Такой сервис должен звать `GlobalAsync.StopAllFor(this)` в своём `Dispose`.

## Запуск

```csharp
AsyncHandle handle = GlobalAsync.Run(this, async token =>
{
    await Awaitable.WaitForSecondsAsync(0.5f, token);
    await FadeOut(token);
}, tag: "fade");

if (handle.IsRunning)
    handle.Stop();
```

`Stop()` идемпотентен: его безопасно звать повторно и после завершения задачи.
`default(AsyncHandle)` — пустой handle, `IsValid == false`.

Владелец — ссылочный объект. `null`, уничтоженный `UnityEngine.Object` и упакованный value
type дают `ArgumentException`: последний каждый раз новая коробка, и `StopAllFor` её не найдёт.

## Теги

Тег уникален в пределах одного владельца: два экземпляра сервиса могут оба держать `"reload"`.
Что делать при повторном запуске, решает `TagConflict`:

| `onConflict` | Поведение |
|---|---|
| `Throw` (по умолчанию) | `InvalidOperationException` — два одинаковых тега почти всегда ошибка |
| `Restart` | старая задача отменяется, новая запускается |
| `Skip` | новая не запускается, возвращается пустой handle |

```csharp
GlobalAsync.Run(this, Reload, "reload", TagConflict.Skip);     // перезарядка уже идёт — игнор
GlobalAsync.Run(this, ShowHint, "hint", TagConflict.Restart);  // новая подсказка заменяет старую

GlobalAsync.StopByTag(this, "reload");
bool reloading = GlobalAsync.IsRunningWithTag(this, "reload");
```

## Короткие формы

```csharp
GlobalAsync.Delay(this, 2f, Explode);                         // через 2 секунды игрового времени
GlobalAsync.Delay(this, 2f, CloseMenu, unscaled: true);       // по реальному времени, и на паузе
GlobalAsync.NextFrame(this, RebuildLayout);
GlobalAsync.WaitUntil(this, () => door.IsOpen, OnDoorOpened,
    timeout: 5f, onTimeout: () => Debug.Log("Дверь не открылась"));
```

Внутри своей задачи — `GlobalAsync.WaitSeconds(seconds, unscaled, token)`: у
`Awaitable.WaitForSecondsAsync` нет режима реального времени.

## Диагностика

```csharp
Debug.Log(GlobalAsync.RunningCount);

var buffer = new List<AsyncJobInfo>();
GlobalAsync.GetRunning(buffer);
foreach (var job in buffer)
    Debug.Log($"{job.Name} | tag: {job.Tag} | {job.Age:F1}s");

GlobalAsync.LogStale(30f); // предупреждение о задачах дольше 30 секунд
```

## Переход с GlobalCoroutine

| GlobalCoroutine | GlobalAsync |
|---|---|
| `StartFor(owner, Routine(), tag)` | `Run(owner, token => Routine(token), tag)` |
| `StartOrRestart(owner, Routine(), tag)` | `Run(owner, ..., tag, TagConflict.Restart)` |
| `StartIfFree(owner, Routine(), tag)` | `Run(owner, ..., tag, TagConflict.Skip)` |
| `CoroutineHandle` | `AsyncHandle` |
| `WaitForSeconds` / `WaitForSecondsUnscaled` | `Delay(owner, seconds, action, unscaled)` |
| `WaitNextFrame` | `NextFrame` |
| `WaitUntil` / `WaitUntilTimeout` | `WaitUntil(..., timeout, onTimeout)` |
| `StopFor(owner, routine)` | `handle.Stop()` |
| `StopAllFor`, `StopByTag`, `IsRunningWithTag`, `RunningCount`, `LogStale` | те же имена |
| `onCancelled` | `try/finally` внутри задачи или `token.Register(...)` |

Тело корутины переписывается так: `yield return new WaitForSeconds(t)` →
`await Awaitable.WaitForSecondsAsync(t, token)`, `yield return null` →
`await Awaitable.NextFrameAsync(token)`, вложенная корутина → `await Nested(token)`.

## Пример

Сэмпл **Utils Samples**, папка `Async`, префаб `GlobalAsyncExample`: `ExampleClass` — сервис подсказок без MonoBehaviour с `TagConflict.Restart` и `Skip`, фоновым потоком через `Awaitable.BackgroundThreadAsync` и `StopAllFor` в `Dispose`; `ExampleGO` — `Run`, `Delay`, `WaitUntil` с таймаутом и остановка при уничтожении объекта.
