# Modular Architecture

Актуальный стандарт архитектуры Box Collector: **modular-first**.

Игра строится из маленьких независимых s&box `Component`. Компонент должен решать одну задачу и быть удобным для добавления через Inspector.

---

## 1. Базовая политика

- Один компонент = одна ответственность.
- Компонент должен быть drag-and-drop friendly.
- Добавил компонент на объект — появились понятные `[Property]` поля в Inspector.
- Настройки группируются через `[Group]`.
- Gameplay, visual, input, network authority и UI не смешиваются в одном компоненте.

---

## 2. Обязательные правила компонента

Каждый компонент обязан:

- использовать `[Property]` для настраиваемых данных;
- использовать `[Group]` для логической группировки настроек;
- проверять зависимости в `OnValidate()` и/или `OnStart()`;
- получать зависимости через `Components.Get<T>()`;
- всегда делать null-check после `Components.Get<T>()`;
- писать понятный `Log.Warning` с именем `GameObject` и названием компонента;
- автоматически добавлять только безопасные зависимости;
- не прятать важные зависимости в коде без Inspector-поля или проверки.

Пример формата предупреждения:

```csharp
Log.Warning( $"{GameObject.Name}: TowerShooter requires TowerTargeting." );
```

---

## 3. Типы компонентов

### Core components

Описывают, что объект такое.

Примеры:

- `TowerCore`
- `EnemyCore`
- `PlaceableObject`

### Behavior components

Описывают, что объект умеет.

Примеры:

- `TowerShooter`
- `EnemyMovement`
- `RespawnOnDeath`

### Visual components

Отвечают только за отображение.

Примеры:

- `HealthBarView`
- `BuildGhostView`
- `SelectionOutline`

### Player tool components

Инструменты игрока.

Примеры:

- `TargetCursor`
- `PlayerInteractionController`
- `PlayerBuildController`

### Manager components

Глобальные системы сцены.

Примеры:

- `GameNetworkManager`
- `WaveManager`
- `EconomyManager`

---

## 4. Сетевые правила

- Input и prediction выполняются только у владельца через `if ( !IsProxy )`.
- Host/server authority используется для здоровья, строительства, экономики и волн.
- `[Sync]` используется только для данных, которые реально нужны другим клиентам.
- `[Rpc.Host]` используется для запросов клиента к хосту.
- `[Rpc.Broadcast]` используется только для визуальных событий, VFX и SFX.
- Клиент не должен напрямую менять authoritative gameplay state.

---

## 5. TargetCursor

`TargetCursor` — специализированный player-owned tool.

Он отвечает за:

- world cursor position;
- network sync позиции курсора;
- hover target;
- визуал курсора.

Он не отвечает за:

- строительство;
- экономику;
- всю interaction-логику игрока;
- правила размещения объектов.

`PlayerInteractionController` читает `TargetCursor`.

Будущий `PlayerBuildController` тоже читает `TargetCursor`.

---

## 6. Здоровье

Не делать один монолитный `UnitHealth` для всех случаев.

Базовый `HealthComponent` отвечает только за:

- HP;
- получение damage;
- состояние death;
- событие смерти.

Отдельными компонентами должны быть:

- respawn;
- уничтожение объекта;
- награды за смерть;
- скрытие модели;
- визуал здоровья.

Примеры:

- `HealthComponent`
- `RespawnOnDeath`
- `DestroyOnDeath`
- `RewardOnDeath`
- `HideModelOnDeath`
- `HealthBarView`

---

## 7. Башни

Башня должна быть набором компонентов:

- `PlaceableObject`
- `TowerCore`
- `TowerTargeting`
- `TowerShooter`
- `HealthComponent`
- optional `InteractableObject`

Правила:

- placement logic не живёт в `TargetCursor`;
- экономика не живёт в башне;
- экономика не живёт в курсоре;
- башня не должна сама списывать деньги;
- башня не должна владеть глобальными правилами строительства.

---

## 8. GameNetworkManager

`GameNetworkManager` отвечает только за:

- lobby;
- network setup;
- player spawning.

В `GameNetworkManager` нельзя добавлять:

- волны;
- экономику;
- башни;
- enemies;
- UI;
- gameplay rules.

Для этих систем нужны отдельные manager components:

- `WaveManager`
- `EconomyManager`
- `BuildManager`
- `EnemySpawnManager`

---

## 9. Главное правило

Если новая логика требует смешать несколько ответственностей в одном компоненте, нужно разделить её на несколько маленьких компонентов.

Компонент должен быть понятен по имени, безопасен при добавлении на объект и предсказуем в Inspector.
