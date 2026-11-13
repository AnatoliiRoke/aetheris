# Задача 00067 — Сценарій бою для офлайн-гравця

> **Статус:** чернетка (чекає погодження автора)
> **Запит автора (12–13.11.2026):** як флот б'ється, коли гравця немає.
> **Логіка бою:** [game-design/06](../game-design/06-kosmichni-boi.md) §10.
> **Обов'язково прочитати:** `documentation/tech/08-pravyla-kodu-dlia-ahentiv.md`, `documentation/tech/09-zvedeni-pravyla-kodu-lohiky-ta-kodiv.md`.
> **Залежить від:** [00065](00065-tsykly-boiu-na-serveri.md), [00071](00071-boiovyi-poriadok-flotu.md).

## 1. Як є зараз (перевірено 13.11.2026)

Після 00065 офлайн-сторона повторює попередні накази або діє за вбудованим «стояти й стріляти за найближчими». Розстановка, склад ескадрилей і розподіл трюму вже беруться з типової формації (00071).

## 2. Рішення

1. **Сценарій** = типова формація флоту (00071) **плюс правила «якщо → то»**; формацій тут не дублюємо.
2. **Правило:** `(умова, оператор, значення) → дія(параметр)`, перевіряються по черзі на початку кожної фази `orders` для офлайн-сторони; спрацьовує **перше**; те саме правило повторно не спрацьовує в одному циклі.
3. **Довідники (сід, адмінка, нейтральні коди, назви в `localization_text`):**

| `scenario_condition_N` | `logic_code` | Значення |
|---|---|---|
| 1 | `force_ratio` | своя сила / сила видимого ворога |
| 2 | `own_loss_pct` | власні втрати, % |
| 3 | `cycle_number` | номер циклу |
| 4 | `carriers_visible` | видно носіїв (0/1) |
| 5 | `drones_visible` | видно дронів (0/1) |
| 6 | `squadron_shield_pct` | щит ескадрильї нижче, % |

| `scenario_action_N` | `logic_code` | Параметр |
|---|---|---|
| 1 | `apply_formation` | `formationId` (лише в фазі `deploy`; у `orders` — відхиляється валідатором збереження) |
| 2 | `retreat_fleet` | — |
| 3 | `retreat_squadron` | `squadronId` |
| 4 | `set_behavior` | `squadronId`, `behaviorCode` |
| 5 | `set_target_rule` | `squadronId`, `targetRuleCode` |
| 6 | `launch_drones` / `recall_drones` | `squadronId` |

Оператори: `<`, `<=`, `>`, `>=`, `=` (ціле з коду-константи `ScenarioOperators`, не довільний рядок).
4. **Сила сторони:** `force = Σ_видимих_кораблів (damage × rate) × wD + Σ(щит+броня+корпус) × wH`, ваги — `combat.force_damage_weight`, `combat.force_defense_weight` (00062). Для ворога — лише по видимих кораблях, і невидимі не враховуються.
5. **Офлайн:** сторона не дала наказів і не натиснула «Готово» у фазі. Гравець, що з'явився, керує з наступної паузи; сценарій не втручається, доки він дає накази.
6. **Шаблони** (сід): «Агресивний» (`own_loss_pct >= 70 → retreat_fleet`), «Обережний» (`force_ratio < 0.7 → retreat_fleet`, `squadron_shield_pct < 30 → retreat_squadron`), «Зберегти флот» (`cycle_number >= 2 → retreat_fleet`); новий флот отримує «Обережний».
7. «Перевірити»: прогін сценарію проти тестового флоту симулятором 00063 на сервері; ліміт — один прогін на гравця за `combat.order_interval_seconds × 5` секунд (використовує той самий rate limit), відповідь — підсумок (результат, втрати, кількість циклів).

## 3. Схема (Combat, міграція `AddScenarios`)

| Таблиця | Колонки | Обмеження |
|---|---|---|
| `combat_scenario` | `fleet_id bigint PK`, `template_code text NULL`, `updated_at timestamptz`, `version int` | |
| `combat_scenario_rule` | `id bigint PK`, `fleet_id bigint`, `position smallint`, `condition_code text`, `operator text`, `value double precision`, `action_code text`, `action_params jsonb` | унікальний `(fleet_id, position)`; `CHECK (operator IN ('<','<=','>','>=','='))`; індекс `(fleet_id)` |
| `scenario_condition`, `scenario_action`, `scenario_template` | `id, code UNIQUE, logic_code, sort_order, is_archived` (шаблон — ще `rules_json`) | сід `combat-reference-seed.json` |

Кількість правил ≤ `combat.scenario_rule_max`.

## 4. API (`/combat/scenarios`, власник флоту)

| Метод і шлях | Запит | Відповідь |
|---|---|---|
| `GET /combat/scenarios/{fleetId}` | — | `{ templateCode, version, rules:[{position,conditionCode,operator,value,actionCode,actionParams}] }` |
| `PUT /combat/scenarios/{fleetId}` | `{ rules:[…], expectedVersion }` | `200`; `400 scenario_rule_invalid` (код, оператор, діапазон значення, `formationId` не цього флоту); `409 scenario_conflict`; `400 scenario_rule_limit` |
| `POST /combat/scenarios/{fleetId}/template` | `{ templateCode }` | `200` (копія правил) |
| `POST /combat/scenarios/{fleetId}/check` | `{ opponent:"mirror"|"weaker"|"stronger" }` | `200 { outcome, myLossesPct, cycles }`; `429` |

## 5. Код

`ScenarioRuleEngine` (чиста функція `Evaluate(rules, BattleView, side) → ScenarioAction[]`, у `Aetheris.Combat`), `ScenarioService` (CRUD + валідація), вхід у `PhaseEndHandler`/початок `orders` для офлайн-сторони. Умови/дії зіставляються з `logic_code`; жодних порогів у коді.

## 6. Тести

Кожна умова й кожна дія окремо; порядок правил, «перше спрацьовує»; гравець, що дав накази, перебиває сценарій; офлайн-детектор; шаблони застосовуються; `apply_formation` лише в `deploy`; чужий `formationId` відхиляється; ліміт правил; «Перевірити» використовує симулятор і не пише в бойові таблиці; невидимі вороги не входять у силу.

## 7. Критерії приймання

1. Флот офлайн-гравця розставляється й діє за сценарієм; шаблони є з коробки.
2. Усі умови й дії — дані з довідників, без чисел у коді.
3. `dotnet build/test/format` зелені.

## 8. Результат виконання

_(заповнює виконавець)_
