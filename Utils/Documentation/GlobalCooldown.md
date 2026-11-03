# GlobalCooldown

`using Utils.Cooldowns;` — сборка `Utils` из пакета `com.existenceisvain.utils`.

Статический трекер **длительных** кулдаунов, которые переживают перезапуск игры:
бустеры, дейли и викли награды, восстановление энергии, «следующий сундук через 4 часа».

Считает время по настенным часам, а не по игровому таймеру, поэтому кулдаун идёт,
пока игра закрыта. Всё состояние лежит в `PlayerPrefs` одним JSON-блобом.

Работает только в главном потоке: `PlayerPrefs` не потокобезопасен.

Записи **не удаляются автоматически**. Истёкший кулдаун остаётся на диске до явного
`Remove` или `Prune` — см. раздел «Очистка».

## GlobalCooldown или FastCooldown

Два похожих класса решают разные задачи. Выбор — по одному вопросу: должен ли таймер
идти, пока игра закрыта?

| | `GlobalCooldown` | `FastCooldown` |
|---|---|---|
| Часы | UTC настенные, монотонные | `Time.time` / `Time.unscaledTime` |
| Переживает выход из игры | да | нет |
| Хранилище | `PlayerPrefs` (JSON) | словарь в памяти |
| Гранулярность | 1 секунда | кадр |
| Пауза (`timeScale = 0`) | идёт всегда | замирает (или нет, при `unscaled`) |
| Ключ | `string` | `CooldownKey` с владельцем |
| Защита от перевода часов | есть | не нужна |
| Типичный масштаб | минуты — дни | доли секунды — минуты |
| Для чего | дейли награды, бустеры, энергия | атака, дэш, антиспам UI |

Атака раз в 1.5 секунды — это `FastCooldown`. Дейли награда — это `GlobalCooldown`.
Использовать `GlobalCooldown` для геймплейных способностей не стоит: каждая запись
идёт на диск, а секундной гранулярности для боя мало.

## Ключи и хранилище

Ключ — обычная строка, глобальная на всю игру. Владельцев, как в `FastCooldown`, здесь
нет намеренно: объект, создавший кулдаун, до следующего запуска не доживёт.

Всё состояние — один ключ `PlayerPrefs`:

```
global_cooldown_store = {
  "version": 1,
  "lastSeenUtc": 1755648000,
  "entries": [
    { "key": "daily_reward", "startUtc": 1755561600, "endUtc": 1755648000 },
    { "key": "xp_booster",   "startUtc": 1755644400, "endUtc": 1755651600 }
  ]
}
```

Один блоб вместо ключа на запись даёт то, чего не было раньше: перечисление ключей,
`Prune`, `ClearAll`, диагностику, атомарную запись и версионирование формата.

Время хранится в unix-секундах UTC — `JsonUtility` не умеет `DateTime`.

## Установка

```csharp
GlobalCooldown.Set("chest", TimeSpan.FromHours(4));

GlobalCooldown.SetSeconds("ad_reward", 90);
GlobalCooldown.SetMinutes("energy", 30);
GlobalCooldown.SetHours("dungeon", 8);
GlobalCooldown.SetDays("weekly_chest", 7);

// До абсолютного момента.
GlobalCooldown.SetUntilUtc("event_end", eventEndUtc);
GlobalCooldown.SetUntilLocal("shop_refresh", new DateTime(2026, 8, 21, 14, 0, 0));
```

Длительность округляется вверх до секунды. `Set` с нулём или отрицательным значением
удаляет запись (кулдаун сразу готов); отрицательное дополнительно пишет предупреждение
в консоль.

## Проверка

```csharp
// Раздельно.
if (GlobalCooldown.IsReady("daily_reward"))
    ClaimDailyReward();

// Проверка + установка одним вызовом (основной паттерн).
if (GlobalCooldown.Check("daily_reward", TimeSpan.FromDays(1)))
    ClaimDailyReward();

// То же, но с остатком при отказе.
if (GlobalCooldown.Check("chest", TimeSpan.FromHours(4), out TimeSpan remaining))
    OpenChest();
else
    ShowHint($"Ещё {GlobalCooldown.Format(remaining)}");
```

`IsReady` **ничего не меняет** — запись живёт до явного удаления, поэтому `GetProgress`,
`GetDuration` и время окончания доступны и после завершения кулдауна.

Отсюда же разница между `Has` и `IsActive`:

```csharp
GlobalCooldown.Has("daily_reward");       // запись существует (истёкшая тоже)
GlobalCooldown.IsActive("daily_reward");  // идёт прямо сейчас
GlobalCooldown.IsReady("daily_reward");   // записи нет или она истекла
```

`Has` отвечает на вопрос «игрок вообще когда-нибудь это забирал?» — полезно, чтобы
отличить первый вход в игру от готовой к клейму награды.

### Check и Set на одном ключе

`Check` — атомарный test-and-set: он не читает состояние, а потребляет его. Два
потребителя одного ключа конфликтуют — окно готовности одно.

```csharp
// НЕПРАВИЛЬНО: первый блок каждый раз перехватывает окно и продлевает кулдаун,
// ClaimDailyReward() не выполнится никогда.
if (GlobalCooldown.IsReady("daily_reward"))
    GlobalCooldown.SetDays("daily_reward", 1);

if (GlobalCooldown.Check("daily_reward", TimeSpan.FromDays(1)))
    ClaimDailyReward();
```

Правило то же, что у `FastCooldown`: у ключа либо одна точка вызова с `Check`, либо
явное ручное управление (`IsReady` для чтения в UI, `Set` в одном месте) — но не оба сразу.

## Ресеты по расписанию

```csharp
GlobalCooldown.SetDailyReset("daily_quest");             // сегодня в 00:00 локального
GlobalCooldown.SetDailyReset("daily_quest", hour: 14);   // ближайшие 14:00 локального
GlobalCooldown.SetDailyResetUtc("season_daily", hour: 4);// 04:00 UTC

GlobalCooldown.SetWeeklyReset("weekly_quest", DayOfWeek.Monday);
GlobalCooldown.SetWeeklyReset("guild_war", DayOfWeek.Friday, hour: 20);

GlobalCooldown.SetMonthlyReset("monthly_pass");             // 1-е число, 00:00
GlobalCooldown.SetMonthlyReset("payday", dayOfMonth: 31);   // клампится: 28/29 в феврале
```

Локальные варианты удобны игроку («ресет в полночь»), UTC-вариант нужен, когда ресет
обязан совпасть у всех игроков независимо от таймзоны — например, у сезонного события,
которое считает сервер. Для монетизируемых ресетов практически всегда правильный
выбор — UTC.

Переход на летнее время обработан: несуществующее локальное время внутри весеннего
провала сдвигается вперёд, осенняя неоднозначность разрешается в стандартное время.
Ни один из ресетов не бросает исключение на границе перевода.

## Монотонные часы и защита от накрутки

Прямой `DateTime.UtcNow` для дейли наград не годится: перевёл часы вперёд → забрал →
перевёл назад → забрал снова. Поэтому класс никогда не читает системное время напрямую.

Эффективное «сейчас» — это `max(системное время, максимальное когда-либо виденное)`.
High-water mark хранится вместе с записями и монотонно не убывает.

```csharp
DateTime now = GlobalCooldown.EffectiveUtcNow;
```

Что это даёт:

- **Откат часов назад больше не работает.** Время не идёт вспять, повторного окна нет.
- **Откат назад ещё и не наказывает игрока.** Кулдаун не «удлиняется» — остаток считается
  от того же монотонного времени.
- **Перевод вперёд офлайн не лечится в принципе.** Одна разовая накрутка сработает —
  клиенту нечем проверить время. Зато после неё вернуть часы назад уже нельзя.

Факт отката фиксируется для аналитики:

```csharp
if (GlobalCooldown.ClockRolledBack)
    Analytics.Report("clock_tamper");
```

### Серверное время

Единственная полная защита — доверенные часы. Подключаются без правки вызовов:

```csharp
public sealed class ServerTime : ICooldownTimeSource
{
    public DateTime UtcNow => _serverUtcAtSync + (DateTime.UtcNow - _localUtcAtSync);
}

GlobalCooldown.SetTimeSource(new ServerTime());  // null — вернуться к системным часам
```

### Когда часы исправили по-настоящему

У high-water mark есть цена. Если у игрока часы стояли в 2030 году и он их исправил,
метка навсегда останется в 2030: относительные длительности продолжат работать верно
(«4 часа» это по-прежнему 4 часа), а привязанные к календарю ресеты — нет.

Escape hatch:

```csharp
GlobalCooldown.ResetClockGuard();
```

Он сбрасывает метку на текущее время. Вызывать **только** когда текущему времени можно
верить — после синхронизации с сервером. Безусловный вызов возвращает дыру с откатом
часов.

## Информация о времени

```csharp
TimeSpan left  = GlobalCooldown.GetTimeRemaining("chest");  // 0 = готов
float progress = GlobalCooldown.GetProgress("xp_booster");  // 0 = только начался, 1 = завершён
TimeSpan total = GlobalCooldown.GetDuration("chest");       // исходная длительность
DateTime endLocal = GlobalCooldown.GetEndTimeLocal("chest");

boosterBar.fillAmount = GlobalCooldown.GetProgress("xp_booster");
label.text = $"До {endLocal:HH:mm}";
```

Полный снимок одним вызовом:

```csharp
if (GlobalCooldown.TryGet("xp_booster", out CooldownInfo info))
    Debug.Log($"{info.Key}: {info.Remaining} из {info.Duration}, {info.Progress:P0}");
```

Для неизвестного ключа `GetProgress` возвращает `1`, `GetTimeRemaining` и `GetDuration` —
ноль, `GetEndTimeUtc` / `GetEndTimeLocal` — `DateTime.MinValue`, `TryGet` — `false`.

## Продление и сокращение

```csharp
GlobalCooldown.Extend("xp_booster", TimeSpan.FromHours(1));   // +1 час за рекламу
GlobalCooldown.Reduce("chest", TimeSpan.FromMinutes(30));     // ускорение за валюту
```

Оба возвращают `false`, если записи нет: продлевать нечего. `Extend` на истёкшей записи
считает от «сейчас», а не от старого конца, поэтому просроченный бустер не воскресает
задним числом. `Reduce` клампится в текущий момент и запись не удаляет — согласовано
с тем, что `IsReady` тоже ничего не удаляет.

## Форматирование

```csharp
string label = GlobalCooldown.GetFormattedTime("chest");
string clock = GlobalCooldown.GetFormattedTime("chest", CooldownFormat.Clock);
string ru    = GlobalCooldown.GetFormattedTime("chest", readyLabel: "Готово!");

// Произвольный интервал, без ключа.
string text = GlobalCooldown.Format(TimeSpan.FromSeconds(9015), CooldownFormat.Full);
```

Режимы на примере остатка `2 дня 5 часов 30 минут 15 секунд`:

| Режим | Вывод | Где уместен |
|---|---|---|
| `Auto` | `2d 5h 30m` | подпись под кнопкой, масштаб подстраивается сам |
| `Short` | `2d` | плашка в углу иконки, мало места |
| `Clock` | `53:30:15` | таймер события, дни свёрнуты в часы |
| `Full` | `2d 05:30:15` | точный таймер с фиксированной шириной |

`readyLabel` по умолчанию `"Ready"` — передавайте локализованную строку. Если нужна
полная локализация формата, берите `GetTimeRemaining` и собирайте текст сами;
`Format` использует `InvariantCulture` и не подстраивается под язык.

## Групповые операции

```csharp
GlobalCooldown.SetMultiple(TimeSpan.FromHours(6), "slot1", "slot2", "slot3");
GlobalCooldown.RemoveMultiple("slot1", "slot2");

if (GlobalCooldown.AllReady("quest_a", "quest_b", "quest_c"))
    ShowClaimAllButton();

if (GlobalCooldown.AnyReady("quest_a", "quest_b"))
    ShowNotificationDot();
```

`SetMultiple` и `RemoveMultiple` пишут на диск один раз, а не по разу на ключ.
На пустом наборе `AllReady` возвращает `true`, `AnyReady` — `false`.

## Очистка

Автоматической очистки нет. Истёкшие записи живут в сторе и не влияют на логику —
только на размер сохранения.

```csharp
GlobalCooldown.Remove("chest");   // один ключ, false если записи не было
int pruned = GlobalCooldown.Prune();  // все истёкшие
GlobalCooldown.ClearAll();        // всё, и в памяти, и в PlayerPrefs
```

`Prune()` здесь безопаснее, чем в `FastCooldown`: скрытого состояния вроде `CheckDelayed`
нет, удаление истёкшей записи ничего не ломает. Но помните, что вместе с записью уходит
и ответ `Has(key)` — если по нему определяется «игрок уже забирал награду», ключ из
`Prune` выпадать не должен.

`ClearAll` намеренно **не** трогает high-water mark часов: сброс метки вернул бы
возможность откатить часы. Для метки есть отдельный `ResetClockGuard`.

## Сохранение

По умолчанию каждая мутация сразу уходит в `PlayerPrefs`. Для пачки изменений запись
можно отложить:

```csharp
GlobalCooldown.AutoSave = false;

foreach (var slot in slots)
    GlobalCooldown.SetHours(slot.Key, slot.Hours);

GlobalCooldown.AutoSave = true;
GlobalCooldown.Save();
```

Флаш также выполняется автоматически на `Application.quitting` и при потере фокуса —
на мобильных это последний надёжный момент перед тем, как процесс убьют. Отдельный
`MonoBehaviour` для этого не нужен, класс остаётся чисто статическим.

`Reload()` выбрасывает состояние из памяти и перечитывает диск, теряя несохранённые
изменения.

Битый стор не роняет игру: он логируется, откладывается в ключ
`global_cooldown_store_corrupt` для разбора, а класс стартует с пустого состояния.
Отдельные некорректные записи (пустой ключ, конец раньше старта, дубликаты)
отбрасываются с предупреждением.

Статика сбрасывается через `RuntimeInitializeOnLoadMethod` на `SubsystemRegistration`,
поэтому состояние не протекает между запусками Play Mode даже при отключённом
Domain Reload.

## Диагностика

```csharp
Debug.Log($"Записей: {GlobalCooldown.Count}, активных: {GlobalCooldown.ActiveCount}");

var buffer = new List<string>();
GlobalCooldown.GetActiveKeys(buffer);
foreach (string key in buffer)
    Debug.Log($"{key} — {GlobalCooldown.GetFormattedTime(key)}");

GlobalCooldown.GetKeys(buffer);  // включая истёкшие
```

`Count` считает все записи вместе с истёкшими, `ActiveCount` — только идущие. Растущий
разрыв означает, что `Prune` не вызывается: на логику это не влияет, но сохранение
пухнет. Обе буферные перегрузки сначала очищают список, чтобы не аллоцировать новый.

## Ручная проверка

- Кулдаун на 8 часов переживает выход из Play Mode и показывает корректный остаток.
- После истечения `IsReady` = `true`, при этом `Has` = `true`, а `GetProgress` = `1`;
  `Prune()` возвращает `1` и после него `Has` = `false`.
- Перевод системных часов назад не увеличивает остаток и выставляет `ClockRolledBack`.
- Перевод вперёд, клейм, возврат назад — повторного окна нет.
- `SetDailyReset` в час перевода на летнее время не бросает исключение.
- Порча `global_cooldown_store` вручную даёт предупреждение, а не краш; исходная строка
  оказывается в `global_cooldown_store_corrupt`.
- При отключённом Reload Domain значения не протекают между запусками Play Mode.

## Отличия от предыдущей версии

- Формат хранения — один JSON-блоб вместо ключа `global_cooldown_<key>` на запись.
  **Старые данные не читаются**; миграции нет, потому что перечислить старые ключи
  невозможно, а класс в проекте не использовался.
- `IsReady` и `GetTimeRemaining` больше не удаляют запись.
- `Clear()` → `ClearAll()`: старый метод чистил только кеш в памяти, оставляя
  `PlayerPrefs` нетронутым.
- `SetCooldown` / `SetCooldownMinutes` / `SetCooldownHours` / `SetCooldownDays` →
  `Set` / `SetMinutes` / `SetHours` / `SetDays`, плюс перегрузка с `TimeSpan`.
- Добавлено время старта записи, а с ним `GetProgress` и `GetDuration`.
- Добавлены монотонные часы, `ICooldownTimeSource`, `ClockRolledBack`, `ResetClockGuard`.
- Добавлены `Check`, `Has`, `IsActive`, `TryGet`, `Extend`, `Reduce`, `SetUntilUtc`,
  `SetUntilLocal`, `SetDailyResetUtc`, `SetWeeklyReset`, `SetMonthlyReset`,
  `SetMultiple`, `RemoveMultiple`, `AllReady`, `AnyReady`, `Prune`, `Save`, `Reload`,
  `AutoSave`, `Count`, `ActiveCount`, `GetKeys`, `GetActiveKeys`.
- `GetFormattedTime` получил режимы вывода и настраиваемый `readyLabel` вместо
  захардкоженного `"Ready"`; добавлен публичный `Format` для произвольных интервалов.
- Исправлены: `long.Parse` без `TryParse`, `PlayerPrefs.Save()` на каждую запись,
  отсутствие сброса статики при выключенном Domain Reload, неоднозначность локального
  времени на переходах DST.

## Пример

Сэмпл **Utils Samples**, папка `GlobalCooldowns`, префаб `GlobalCooldownExample`: `ExampleClass` — ежедневная награда со сбросом в 05:00 и энергия на 30 минут; `ExampleGO` — сундук: крышка открыта, пока награду можно забрать; кнопки и статус на панели демо. Таймеры переживают перезапуск Play Mode.
