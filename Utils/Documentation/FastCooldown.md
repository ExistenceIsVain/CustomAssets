# FastCooldown

`using Utils.Cooldowns;` — сборка `Utils` из пакета `com.existenceisvain.utils`.

Статический трекер кулдаунов по ключу. Не требует MonoBehaviour, не тикает
каждый кадр — время вычисляется по запросу от момента установки.

Работает только в главном потоке: используется `Time.time` / `Time.unscaledTime`.

Записи **не удаляются автоматически**. Очистка полностью ручная — см. раздел
«Очистка».

## Ключи

Ключ — структура `CooldownKey`. Есть неявное преобразование из строки, поэтому
простой вызов выглядит как обычно:

```csharp
FastCooldown.Set("attack", 1.5f);
```

Такой ключ **глобальный**: все объекты с именем `"attack"` делят один кулдаун.
Для множественных сущностей (враги, оружие, юниты) ключ нужно привязать
к владельцу:

```csharp
public class Enemy : MonoBehaviour
{
    private CooldownKey _attackKey;

    private void Awake()
    {
        _attackKey = new CooldownKey(this, "attack");
    }

    private void Update()
    {
        if (CanSeePlayer && FastCooldown.Check(_attackKey, 1.5f))
            Attack();
    }

    private void OnDestroy()
    {
        FastCooldown.RemoveAllFor(this);
    }
}
```

Владелец сравнивается по ссылке, а не по `Equals`, поэтому уничтоженные
Unity-объекты не схлопываются в один ключ. Ключ стоит кэшировать в поле:
создание строки и структуры на каждый кадр — лишние аллокации.

Владельцем может быть любой `object`, не только MonoBehaviour.

## Масштабированное и реальное время

По умолчанию кулдаун идёт по `Time.time`, то есть **замирает на паузе**
(`timeScale = 0`) и замедляется в slow-motion. Для геймплейных способностей это
правильное поведение.

Флаг `unscaled: true` переключает на `Time.unscaledTime` — для UI-антиспама,
автосейва, сетевых ретраев и всего, что должно тикать на паузе:

```csharp
FastCooldown.Set("ui_click", 0.2f, unscaled: true);
```

Режим запоминается в записи, поэтому `GetTimeRemaining` и `GetProgress`
читают правильные часы автоматически. Смена режима возможна только через
повторный `Set`.

## Установка и проверка

```csharp
// Раздельно.
FastCooldown.Set("attack", 1.5f);
if (FastCooldown.IsReady("attack"))
    Attack();

// Проверка + установка одним вызовом (основной паттерн).
if (FastCooldown.Check("attack", 1.5f))
    Attack();

// То же, но с остатком времени при отказе.
if (FastCooldown.Check("shoot", 0.5f, out float remaining))
    Shoot();
else
    ShowCooldownHint(remaining);
```

`IsReady` не изменяет состояние — данные о завершённом кулдауне остаются
доступны для `GetProgress` и `GetDuration` до явного удаления.

`Set` с `duration <= 0` удаляет запись (кулдаун сразу готов). Отрицательное
значение дополнительно пишет предупреждение в консоль.

### Check и Set на одном ключе

`Check` — атомарный test-and-set: он не читает состояние, а потребляет его.
Два потребителя одного ключа в одном кадре конфликтуют — окно готовности одно.

```csharp
// НЕПРАВИЛЬНО: первый блок каждый раз перехватывает окно готовности
// и продлевает кулдаун, Attack() не выполнится никогда.
if (FastCooldown.IsReady("attack"))
    FastCooldown.Set("attack", 1.5f);

if (FastCooldown.Check("attack", 1.5f))
    Attack();
```

Правило: у ключа либо одна точка вызова с `Check`, либо явное ручное
управление (`IsReady` для чтения в UI, `Set` в одном месте) — но не оба сразу.

### CheckDelayed — первый вызов ожидает

`Check` срабатывает сразу при первом обращении. `CheckDelayed` наоборот:
первый вызов взводит кулдаун и возвращает `false`, действие выполняется только
начиная со второго окна.

```csharp
if (FastCooldown.CheckDelayed(_attackKey, 1.5f))
    Attack();
```

Работает на том, что **наличие записи** и есть отметка «уже взводился».
Поэтому логика сбрасывается, если запись удалить:

- `Prune()` сотрёт истёкшую запись → следующий вызов снова взведёт и вернёт
  `false`;
- `Remove` / `RemoveAllFor` / `Clear` — то же самое;
- `Set` с `duration <= 0` удаляет запись, то есть тоже сбрасывает взвод.

Это не баг, а цена ручной очистки: удаление записи означает «начать с нуля».
Если такое поведение нежелательно, альтернатива — обычный `Check` плюс `Set`
в момент появления сущности:

```csharp
private void OnEnable() => FastCooldown.Set(_attackKey, 1.5f);
```

## Информация о времени

```csharp
float left     = FastCooldown.GetTimeRemaining("attack"); // секунд осталось, 0 = готов
float progress = FastCooldown.GetProgress("attack");      // 0 = только начался, 1 = завершён
float total    = FastCooldown.GetDuration("attack");      // исходная длительность
bool active    = FastCooldown.IsActive("attack");         // идёт прямо сейчас
bool unscaled  = FastCooldown.IsUnscaled("attack");

cooldownBar.value = FastCooldown.GetProgress(_attackKey);
```

Для несуществующего ключа `GetProgress` возвращает `1`, `GetTimeRemaining`
и `GetDuration` — `0`.

## Групповые операции

```csharp
FastCooldown.SetMultiple(2f, "skill1", "skill2", "skill3");
FastCooldown.SetMultiple(2f, unscaled: true, "ui_a", "ui_b");

FastCooldown.RemoveMultiple("skill1", "skill2");

if (FastCooldown.AllReady("skill1", "skill2", "skill3"))
    CastCombo();

if (FastCooldown.AnyReady("skill1", "skill2"))
    CastAny();
```

На пустом наборе `AllReady` возвращает `true`, `AnyReady` — `false`.

## Очистка

Автоматической очистки нет. Истёкшие записи живут в словаре до явного удаления
и не влияют на логику — только на память.

```csharp
FastCooldown.Remove("attack");        // один ключ
FastCooldown.RemoveAllFor(this);      // все ключи владельца
FastCooldown.Clear();                 // всё
FastCooldown.Prune();                 // только истёкшие
```

`RemoveAllFor(this)` в `OnDestroy` / `Dispose` владельца — **обязательная
дисциплина**, а не рекомендация. Это единственный штатный путь очистки для
owner-ключей: забытый вызов означает, что словарь копит мёртвые записи
до конца сессии.

`Prune()` — адресная операция, а не безусловная уборка. Она сбрасывает
состояние `CheckDelayed` у всех ключей, чьи кулдауны истекли, поэтому вызывать
её «на всякий случай» при смене сцены не стоит. Если `CheckDelayed` в проекте
не используется, `Prune()` безопасен в любой момент.

Статическое состояние сбрасывается через `RuntimeInitializeOnLoadMethod`
на `SubsystemRegistration`, поэтому кулдауны не переживают выход из Play Mode
даже при отключённом Domain Reload.

## Диагностика

```csharp
Debug.Log($"Записей: {FastCooldown.Count}");

var buffer = new List<CooldownKey>();
FastCooldown.GetActiveKeys(buffer);
foreach (var key in buffer)
    Debug.Log($"{key} — осталось {FastCooldown.GetTimeRemaining(key):F2}с");
```

`GetActiveKeys` принимает буфер, чтобы не аллоцировать список на каждый вызов.
Он возвращает только активные ключи, а `Count` — общее число записей вместе
с истёкшими. Растущий разрыв между ними означает забытый `RemoveAllFor`.

## Отличия от предыдущей версии

- `Exists` → `IsActive` (учитывает истечение, а не просто наличие записи).
- `TryCheck` → перегрузка `Check` с `out remaining`.
- `Clear("destroy")` → `Clear()`.
- `IsReady` больше не удаляет запись.
- Убран префикс ключей — он аллоцировал строку на каждый вызов.
- Добавлены `CooldownKey` с владельцем, флаг `unscaled`, `CheckDelayed`,
  `RemoveAllFor`, `Prune`, `IsUnscaled`, `Count`, `GetActiveKeys`.

## Пример

Сэмпл **Utils Samples**, папка `FastCooldowns`, префаб `FastCooldownExample`: `ExampleClass` — оружие без MonoBehaviour с ключом-владельцем и `RemoveAllFor` в `Dispose`; `ExampleGO` — атака и рывок по реальному времени, прогресс на экране, `RemoveAllFor` в `OnDestroy`.
