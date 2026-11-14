# Бій

Модуль тримає налаштування бою і збирає стан корабля з флоту та каталогу. Розрахунок відрізку живе в бібліотеці `Aetheris.Combat.Engine`: без бази, мережі й годинника. Консольний симулятор `server/tools/Aetheris.CombatSim` викликає лише цю бібліотеку.

## Запуск симулятора

З кореня репозиторію:

```
dotnet run --project server/tools/Aetheris.CombatSim -- --fleet-a server/tools/balance/combat/fleet-a.json --fleet-b server/tools/balance/combat/fleet-b.json --runs 200 --seed 1 --behavior-a approach --behavior-b keep_distance --out server/tools/balance/combat/report-001.md
```

Поруч зі звітом пишеться CSV з тим самим ім'ям. Проєкти читаються з `server/tools/balance/combat/designs.json`. Якщо поточної теки немає цього шляху, файл шукається вгору по каталогах. Свій шлях задає `--designs`.

Без `--runs` буде 200 прогонів, без `--seed` — зерно 1. Прогін k має зерно першого плюс k. Поведінки: `approach`, `keep_distance`, `stand`, `escort`. Правило цілі лежить у каталозі.

`--benchmark` друкує час і виділені байти одного відрізку завантажених флотів і не ганяє серію. Звичайний звіт теж записує один такий вимір, окремо від серії.

Файл флоту: `ships` — список `{ "designId", "count" }`, `formation` — `originX`, `originY`, `spacingX`, `spacingY`. Перший корабель стоїть у початку, кожен наступний зсувається на крок.

Перший звіт цього вивантаження: `server/tools/balance/combat/report-001.md`.
