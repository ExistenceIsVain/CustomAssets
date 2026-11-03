# Transmitter

`using Utils.Messaging;` — сборка `Utils` из пакета `com.existenceisvain.utils`.

`Transmitter` — статическая шина сообщений для синхронного взаимодействия между частями игры без прямых ссылок между отправителем и получателем.

Поддерживаются два сценария:

- `IReceiver<T>` — широковещательная отправка события всем зарегистрированным получателям;
- `IProvider<TRequest, TResponse>` — синхронный запрос данных у зарегистрированного провайдера.

Все вызовы выполняются сразу в вызывающем потоке. Очереди сообщений, задержки и фоновой обработки нет. Реестр не потокобезопасен; используйте `Transmitter` из главного потока Unity.

## События: `IReceiver<T>`

Тип сообщения определяет отдельный канал:

```csharp
public readonly struct PlayerDiedEvent
{
    public PlayerDiedEvent(int score) => Score = score;

    public int Score { get; }
}
```

Получатель реализует `IReceiver<T>` и регистрируется на время своей активности:

```csharp
public sealed class HudController : MonoBehaviour, IReceiver<PlayerDiedEvent>
{
    private void OnEnable()
    {
        Transmitter.Register<PlayerDiedEvent>(this);
    }

    private void OnDisable()
    {
        Transmitter.Unregister<PlayerDiedEvent>(this);
    }

    public void Receive(PlayerDiedEvent message)
    {
        Debug.Log($"Score: {message.Score}");
    }
}
```

Отправка синхронно вызывает всех живых получателей этого типа:

```csharp
Transmitter.Send(new PlayerDiedEvent(score: 42));
```

Обработчики вызываются в порядке, обратном порядку хранения. Не полагайтесь на этот порядок как на приоритет: API не предоставляет контракт сортировки.

Исключение одного получателя записывается в Unity Console и не мешает вызвать остальных. `null`-сообщение не отправляется и приводит к предупреждению.

## Запросы: `IProvider<TRequest, TResponse>`

Создайте отдельные типы запроса и ответа:

```csharp
public readonly struct PlayerStatsRequest { }

public readonly struct PlayerStatsResponse
{
    public PlayerStatsResponse(int health, int mana)
    {
        Health = health;
        Mana = mana;
    }

    public int Health { get; }
    public int Mana { get; }
}
```

Провайдер реализует `IProvider<TRequest, TResponse>`:

```csharp
public sealed class PlayerStats : MonoBehaviour,
    IProvider<PlayerStatsRequest, PlayerStatsResponse>
{
    private void OnEnable()
    {
        Transmitter.Register<PlayerStatsRequest, PlayerStatsResponse>(
            this,
            single: true);
    }

    private void OnDisable()
    {
        Transmitter.Unregister<PlayerStatsRequest, PlayerStatsResponse>(this);
    }

    public PlayerStatsResponse Provide(PlayerStatsRequest request)
    {
        return new PlayerStatsResponse(health: 100, mana: 50);
    }
}
```

Основной вариант запроса возвращает признак успеха отдельно от результата:

```csharp
if (Transmitter.Request<PlayerStatsRequest, PlayerStatsResponse>(
        new PlayerStatsRequest(),
        out var stats))
{
    Debug.Log($"Health: {stats.Health}");
}
```

Также доступны callback и краткая форма:

```csharp
Transmitter.Request<PlayerStatsRequest, PlayerStatsResponse>(
    new PlayerStatsRequest(),
    stats => Debug.Log($"Mana: {stats.Mana}"));

PlayerStatsResponse? stats =
    Transmitter.Ask<PlayerStatsRequest, PlayerStatsResponse>(
        new PlayerStatsRequest());
```

`Ask` возвращает `default`, если получить ответ не удалось. Для value type значение `default` может быть корректным ответом, поэтому в коде, где важно отличить ошибку, используйте `Request(..., out response)`.

Если провайдер отсутствует, `Request` по умолчанию пишет ошибку. Параметр `silent: true` убирает её совсем — для необязательных запросов, где отсутствие провайдера нормально. `silent` есть и у `Request` с callback, и у `Ask`.

Провайдер ищется по паре типов запроса и ответа: `IProvider<PlayerStatsRequest, PlayerStatsResponse>` и `IProvider<PlayerStatsRequest, int>` — разные каналы и не мешают друг другу. Исключение внутри `Provide` записывается в Console; после этого `Transmitter` пробует следующий зарегистрированный провайдер.

## Единственная регистрация и замена

Параметры `single` и `replace` одинаково работают для receivers и providers:

```csharp
Transmitter.Register<PlayerDiedEvent>(
    this,
    single: true,
    replace: false);
```

- `single: false` — разрешает несколько объектов на канале;
- `single: true, replace: false` — оставляет существующую регистрацию и игнорирует новую;
- `single: true, replace: true` — удаляет все существующие регистрации канала и добавляет новую.

Для провайдера, от которого ожидается единственный авторитетный ответ, обычно подходит `single: true`. Без него `Request` обращается к последнему зарегистрированному живому провайдеру.

## Жизненный цикл и ссылки

Для `MonoBehaviour` стандартная схема — `Register` в `OnEnable` и симметричный `Unregister` в `OnDisable`. Тип `T` при снятии регистрации должен совпадать с типом при регистрации.

Runtime-реестры хранят сильные ссылки. Уничтоженный `UnityEngine.Object` распознаётся и удаляется при отправке, запросе или ручной очистке, но обычный C#-объект останется в памяти, пока не будет явно снят с регистрации.

Обычные сервисные классы также поддерживаются:

```csharp
public sealed class AnalyticsReceiver : IReceiver<PlayerDiedEvent>, IDisposable
{
    public AnalyticsReceiver(MonoBehaviour owner)
    {
        Transmitter.Register<PlayerDiedEvent>(this, creator: owner);
    }

    public void Receive(PlayerDiedEvent message)
    {
        // ...
    }

    public void Dispose()
    {
        Transmitter.Unregister<PlayerDiedEvent>(this);
    }
}
```

Аргумент `creator` не управляет временем жизни регистрации. Он нужен только монитору, чтобы связать обычный C#-объект с GameObject. Владелец всё равно обязан вызвать `Unregister`/`Dispose`.

## Очистка

```csharp
Transmitter.CleanupDeadReferences();
Transmitter.ClearAll();
```

- `CleanupDeadReferences` удаляет уничтоженные Unity-объекты из runtime-реестров и очищает соответствующие данные мониторинга;
- `ClearAll` снимает все регистрации receivers и providers и оповещает подписчиков `OnRegistryChanged`.

Статические реестры и подписчики `OnRegistryChanged` автоматически сбрасываются через `SubsystemRegistration` при запуске Play Mode. Поэтому состояние предыдущей сессии не сохраняется даже при отключённом Domain Reload.

## Монитор в Editor

Окно открывается через `Tools > Transmitter Monitor` и показывает:

- receivers и providers, сгруппированные по типу сообщения или запроса;
- класс и GameObject зарегистрированного объекта;
- время регистрации и состояние ссылки;
- переход к скрипту или GameObject по клику;
- автоматическое обновление, очистку мёртвых ссылок и полную очистку реестра.

Записи монитора ведутся только в редакторе: в сборке игры `Register` не выделяет под них память, а `OnRegistryChanged`, `GetReceiverEntries` и `GetProviderEntries` недоступны. Повторная регистрация того же объекта (`OnEnable` после `OnDisable`) оживляет его старую запись, а мёртвых записей на канал хранится не больше 32 — список не растёт от спавна и уничтожения врагов.

`OnRegistryChanged` можно использовать для собственной диагностики в редакторе (код под `#if UNITY_EDITOR`):

```csharp
private void OnEnable() => Transmitter.OnRegistryChanged += Refresh;
private void OnDisable() => Transmitter.OnRegistryChanged -= Refresh;
```

Методы `GetReceiverEntries()` и `GetProviderEntries()` предназначены для мониторинга. Не изменяйте возвращённые списки: словарь копируется, но содержащиеся в нём `List<TransmitterEntry>` принадлежат внутреннему состоянию `Transmitter`.

## Краткий справочник API

| Метод | Назначение |
| --- | --- |
| `Register<T>(receiver, ...)` | Зарегистрировать получателя события `T` |
| `Unregister<T>(receiver)` | Снять регистрацию получателя |
| `Send<T>(data)` | Синхронно отправить событие всем получателям |
| `Register<TRequest, TResponse>(provider, ...)` | Зарегистрировать провайдера |
| `Unregister<TRequest, TResponse>(provider)` | Снять регистрацию провайдера |
| `Request<TRequest, TResponse>(request, out response, silent)` | Запросить ответ с явным признаком успеха |
| `Request<TRequest, TResponse>(request, callback)` | Запросить ответ и вызвать callback при успехе |
| `Ask<TRequest, TResponse>(request)` | Получить ответ либо `default` |
| `CleanupDeadReferences()` | Удалить мёртвые ссылки |
| `ClearAll()` | Полностью очистить реестры |

## Ограничения

- Только синхронное выполнение; для асинхронных ответов передавайте callback/task в собственном типе сообщения либо используйте отдельный механизм.
- Нет приоритетов и гарантированного пользовательского порядка вызова.
- Нет потокобезопасности.
- Нет автоматического снятия регистрации для обычных C#-объектов.
- Тип запроса должен однозначно определять тип ответа.

## Пример

Сэмпл **Utils Samples**, папка `Messaging`, префаб `TransmitterExample`: `ExampleClass` — сервис счёта без MonoBehaviour, одновременно получатель `EnemyKilled` и провайдер `ScoreRequest → int`; `ExampleGO` — HUD, который регистрируется на время активности, шлёт событие и спрашивает счёт, в том числе у отсутствующего провайдера с `silent: true`.
