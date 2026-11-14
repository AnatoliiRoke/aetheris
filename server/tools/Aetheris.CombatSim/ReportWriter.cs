using System.Globalization;
using System.Text;

namespace Aetheris.CombatSim;

/// <summary>Everything the report needs. The numbers were measured, not typed.</summary>
internal sealed class ReportRequest
{
    public required string Arguments { get; init; }

    public required string DesignsPath { get; init; }

    public required DesignCatalog Catalog { get; init; }

    public required FleetFile FleetA { get; init; }

    public required FleetFile FleetB { get; init; }

    public required string BehaviorA { get; init; }

    public required string BehaviorB { get; init; }

    public ulong Seed { get; init; }

    public int Runs { get; init; }

    public required RunSet Headline { get; init; }

    public required OpeningPicture Opening { get; init; }

    public required IReadOnlyList<MatrixCell> Matrix { get; init; }

    public required SegmentSample Sample { get; init; }
}

/// <summary>Writes the Markdown report and the CSV beside it.</summary>
internal static class ReportWriter
{
    private const double PercentScale = 100;

    private const string PercentFormat = "0.00";

    private const string NumberFormat = "0.###";

    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    public static void Write(string reportPath, string csvPath, ReportRequest request)
    {
        EnsureDirectory(reportPath);
        EnsureDirectory(csvPath);
        File.WriteAllText(reportPath, Markdown(request), Utf8);
        File.WriteAllText(csvPath, Csv(request), Utf8);
    }

    private static void EnsureDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string Markdown(ReportRequest request)
    {
        StringBuilder report = new();
        DesignCatalog catalog = request.Catalog;
        RunSet headline = request.Headline;
        Line(report, "# Звіт 001. Бій флотів");
        Line(report, "");
        Line(report, "Симулятор прогнав рушій бою. Числа нижче зняті з цього запуску.");
        Line(report, "");
        Line(report, $"Аргументи: `{request.Arguments}`");
        Line(report, $"Проєкти: `{request.DesignsPath}`");

        if (catalog.Source.Length > 0)
        {
            Line(report, $"Сід, з якого зібрано проєкти: `{catalog.Source}`");
        }

        Line(report, $"Зерно першого прогону: {request.Seed.ToString(CultureInfo.InvariantCulture)}. Прогін k має зерно першого плюс k.");
        Line(report, $"Прогонів: {request.Runs.ToString(CultureInfo.InvariantCulture)}.");
        Line(report, $"Поведінка A: `{request.BehaviorA}`. Поведінка B: `{request.BehaviorB}`. Правило цілі: `{catalog.TargetRule}`.");
        Line(report, $"Сторони: A = {catalog.Layout.SideA.ToString(CultureInfo.InvariantCulture)}, B = {catalog.Layout.SideB.ToString(CultureInfo.InvariantCulture)}.");
        Line(report, "Цикли йдуть від нуля до ліміту. Рушій сам номер циклу не збільшує. Якщо після останнього відрізку обидві сторони ще на полі, залік — нічия.");
        Line(report, "Втрати — це знищені кораблі і вартість їхніх проєктів. Пошкоджений, але живий корабель втратою не рахується.");
        Line(report, "");
        Line(report, "## Флоти");
        Line(report, "");
        Line(report, "| Сторона | Проєкт | Кораблів | Вартість одного | Вартість сторони |");
        Line(report, "| --- | --- | ---: | ---: | ---: |");
        WriteFleet(report, "A", request.FleetA, catalog);
        WriteFleet(report, "B", request.FleetB, catalog);
        Line(report, "");
        Line(report, FormationLine("A", request.FleetA));
        Line(report, FormationLine("B", request.FleetB));
        Line(report, "");
        Line(report, "## Старт");
        Line(report, "");
        Line(report, OpeningLine(request.Opening));
        Line(report, "Виявлення пари — добуток виявлення спостерігача, сигнатури цілі і (1 − зріз сигнатури). Планет на полі немає, тож лінія огляду вільна. Обидва напрямки пари мають бачити один одного.");
        Line(report, "");
        Line(report, "## Підсумок");
        Line(report, "");
        Line(report, "| Сторона | Перемоги | Частка, % | Середні втрати, кораблі | Середні втрати, вартість | Середні постріли | Середні влучання |");
        Line(report, "| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
        Line(report, SummaryRow("A", headline.WinsA, headline.Runs, headline.ShipsLostA, headline.CostLostA, headline.ShotsA, headline.HitsA));
        Line(report, SummaryRow("B", headline.WinsB, headline.Runs, headline.ShipsLostB, headline.CostLostB, headline.ShotsB, headline.HitsB));
        Line(report, $"| Нічия | {headline.Draws.ToString(CultureInfo.InvariantCulture)} | {Share(headline.Draws, headline.Runs)} |  |  |  |  |");
        Line(report, "");
        Line(report, $"Середня тривалість: {Mean((double)headline.CycleSum, headline.Runs)} циклів. Ліміт: {catalog.Settings.MaxCycles.ToString(CultureInfo.InvariantCulture)}.");
        Line(report, $"Після першого прогону найменша відстань між кораблями різних сторін: {Number(headline.FirstRunClosest)}.");
        Line(report, "");
        Line(report, "| Циклів | Прогонів |");
        Line(report, "| ---: | ---: |");

        for (int cycles = 1; cycles < headline.ByCycles.Count; cycles++)
        {
            Line(report, $"| {cycles.ToString(CultureInfo.InvariantCulture)} | {headline.ByCycles[cycles].ToString(CultureInfo.InvariantCulture)} |");
        }

        Line(report, "");
        Line(report, "## Матриця проєкт проти проєкту");
        Line(report, "");
        Line(report, "Один корабель проти одного, ті самі поведінки, зерно і число прогонів. Точки дуелі беруться з каталогу. Клітинка — частка перемог рядка, у відсотках.");
        Line(report, "");
        WriteMatrix(report, request);
        Line(report, "");
        Line(report, DuelLine(catalog));
        WriteReaches(report, catalog);
        Line(report, "");
        Line(report, "## Влучність по корпусах цих проєктів");
        Line(report, "");
        Line(report, "| Зброя | По групі | Шанс |");
        Line(report, "| --- | --- | ---: |");
        WriteAccuracy(report, catalog);
        Line(report, "");
        Line(report, "## Модулі");
        Line(report, "");
        Line(report, "Шкода береться із зведення `fire_summary` (щит + броня + корпус) головних флотів і ділиться між зброєю ескадрильї пропорційно до добутку шкоди залпу і темпу. Вартість деталі — сума кількостей інгредієнтів її рецепта. Шкода на одиницю вартості — середня шкода за прогін, поділена на вартість усіх копій деталі в бою.");
        Line(report, "");
        Line(report, "| Деталь | Назва | Копій | Вартість однієї | Середня шкода за прогін | Шкода на одиницю вартості |");
        Line(report, "| --- | --- | ---: | ---: | ---: | ---: |");
        WriteModules(report, headline);
        Line(report, "");
        Line(report, BestModule(headline));
        Line(report, "");
        Line(report, "## Один відрізок");
        Line(report, "");
        Line(report, $"Після прогріву тим самим відрізком один відрізок головних флотів зайняв {request.Sample.Milliseconds.ToString(NumberFormat, CultureInfo.InvariantCulture)} мс і виділив {request.Sample.AllocatedBytes.ToString(CultureInfo.InvariantCulture)} байт на поточному потоці. Це вимір цього запуску, не літерал у коді. Окремий прапорець `--benchmark` друкує той самий вимір і не ганяє серію.");
        Line(report, "");
        return report.ToString();
    }

    private static void WriteFleet(StringBuilder report, string side, FleetFile fleet, DesignCatalog catalog)
    {
        for (int i = 0; i < fleet.Groups.Count; i++)
        {
            FleetGroup group = fleet.Groups[i];
            ShipDesign design = catalog.Require(group.DesignId);
            Line(
                report,
                $"| {side} | {design.Name} (`{design.Id}`) | {group.Count.ToString(CultureInfo.InvariantCulture)} | {Number(design.Cost)} | {Number(design.Cost * group.Count)} |");
        }
    }

    private static string FormationLine(string side, FleetFile fleet)
    {
        FleetFormation formation = fleet.Formation;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Сторона {side}: початок ({Number(formation.OriginX)}, {Number(formation.OriginY)}), крок ({Number(formation.SpacingX)}, {Number(formation.SpacingY)}).");
    }

    private static string OpeningLine(OpeningPicture opening)
    {
        string reach = double.IsFinite(opening.MinReach) ? Number(opening.MinReach) : "немає";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Пар корабель–корабель: {opening.Pairs.ToString(CultureInfo.InvariantCulture)}. Бачать одна одну: {opening.Seen.ToString(CultureInfo.InvariantCulture)}. Найбільша відстань: {Number(opening.MaxDistance)}. Найменша дальність виявлення пари: {reach}. Найбільша дальність зброї A: {Number(opening.MaxRangeA)}, B: {Number(opening.MaxRangeB)}.");
    }

    private static string SummaryRow(string side, int wins, int runs, int ships, double cost, long shots, long hits)
    {
        return $"| {side} | {wins.ToString(CultureInfo.InvariantCulture)} | {Share(wins, runs)} | {Mean(ships, runs)} | {Mean(cost, runs)} | {Mean(shots, runs)} | {Mean(hits, runs)} |";
    }

    private static void WriteMatrix(StringBuilder report, ReportRequest request)
    {
        List<string> columns = [];

        for (int i = 0; i < request.Matrix.Count; i++)
        {
            string columnId = request.Matrix[i].ColumnId;

            if (!columns.Contains(columnId))
            {
                columns.Add(columnId);
            }
        }

        StringBuilder header = new("| Рядок (A) \\ стовпець (B) |");
        StringBuilder rule = new("| --- |");

        for (int i = 0; i < columns.Count; i++)
        {
            header.Append(" `");
            header.Append(columns[i]);
            header.Append("` |");
            rule.Append(" ---: |");
        }

        Line(report, header.ToString());
        Line(report, rule.ToString());
        string? currentRow = null;
        StringBuilder row = new();

        for (int i = 0; i < request.Matrix.Count; i++)
        {
            MatrixCell cell = request.Matrix[i];

            if (!string.Equals(currentRow, cell.RowId, StringComparison.Ordinal))
            {
                if (currentRow is not null)
                {
                    Line(report, row.ToString());
                }

                currentRow = cell.RowId;
                row.Clear();
                row.Append("| `");
                row.Append(cell.RowId);
                row.Append("` |");
            }

            row.Append(' ');
            row.Append(Share(cell.Runs.WinsA, cell.Runs.Runs));
            row.Append(" |");
        }

        if (currentRow is not null)
        {
            Line(report, row.ToString());
        }
    }

    private static string DuelLine(DesignCatalog catalog)
    {
        double dx = catalog.Duel.OriginBX - catalog.Duel.OriginAX;
        double dy = catalog.Duel.OriginBY - catalog.Duel.OriginAY;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Відстань між точками дуелі: {Number(distance)}.");
    }

    private static void WriteReaches(StringBuilder report, DesignCatalog catalog)
    {
        for (int observer = 0; observer < catalog.Designs.Count; observer++)
        {
            for (int target = 0; target < catalog.Designs.Count; target++)
            {
                ShipDesign from = catalog.Designs[observer];
                ShipDesign to = catalog.Designs[target];
                Line(report, $"Виявлення `{from.Id}` по `{to.Id}`: {Number(Reach(from, to))}.");
            }
        }
    }

    private static void WriteAccuracy(StringBuilder report, DesignCatalog catalog)
    {
        for (int designIndex = 0; designIndex < catalog.Designs.Count; designIndex++)
        {
            ShipDesign design = catalog.Designs[designIndex];

            for (int moduleIndex = 0; moduleIndex < design.Modules.Count; moduleIndex++)
            {
                FittedModule module = design.Modules[moduleIndex];

                if (module.Kind != Aetheris.Combat.Engine.ModuleKinds.Weapon)
                {
                    continue;
                }

                for (int targetIndex = 0; targetIndex < catalog.Designs.Count; targetIndex++)
                {
                    ShipDesign target = catalog.Designs[targetIndex];
                    string chance = Chance(module, target.HullGroupId);
                    Line(report, $"| `{module.Part}` | `{target.HullGroup}` ({target.HullGroupName}) | {chance} |");
                }
            }
        }
    }

    private static string Chance(FittedModule module, int hullGroupId)
    {
        for (int i = 0; i < module.Accuracy.Count; i++)
        {
            if (module.Accuracy[i].HullGroupId == hullGroupId)
            {
                return Number(module.Accuracy[i].Chance);
            }
        }

        return "немає рядка";
    }

    private static void WriteModules(StringBuilder report, RunSet headline)
    {
        if (headline.Modules.Count == 0)
        {
            Line(report, "|  |  |  |  |  |  |");
            return;
        }

        for (int i = 0; i < headline.Modules.Count; i++)
        {
            ModuleScore score = headline.Modules[i];
            Line(
                report,
                $"| `{score.Part}` | {score.Name} | {score.Copies.ToString(CultureInfo.InvariantCulture)} | {Number(score.Cost)} | {Number(score.MeanDamage)} | {Number(score.DamagePerCost)} |");
        }
    }

    private static string BestModule(RunSet headline)
    {
        if (headline.Modules.Count == 0 || headline.Modules[0].DamagePerCost <= 0)
        {
            return "Жоден модуль не завдав шкоди.";
        }

        ModuleScore best = headline.Modules[0];

        return $"Найбільша шкода на одиницю вартості: {best.Name} (`{best.Part}`), {Number(best.DamagePerCost)}.";
    }

    private static double Reach(ShipDesign observer, ShipDesign target)
    {
        double detect = 0;

        for (int i = 0; i < observer.Modules.Count; i++)
        {
            if (observer.Modules[i].Facts.Detection > detect)
            {
                detect = observer.Modules[i].Facts.Detection;
            }
        }

        double factor = target.Signature * (1 - target.SignatureCut);

        if (factor < 0)
        {
            factor = 0;
        }

        return detect * factor;
    }

    private static string Csv(ReportRequest request)
    {
        StringBuilder table = new();
        DesignCatalog catalog = request.Catalog;
        RunSet headline = request.Headline;
        Line(table, "section,a,b,c,d,e,f,g,h");
        Line(table, $"meta,{CsvText(request.Seed.ToString(CultureInfo.InvariantCulture))},{request.Runs.ToString(CultureInfo.InvariantCulture)},{CsvText(request.BehaviorA)},{CsvText(request.BehaviorB)},{CsvText(catalog.TargetRule)},{CsvText(request.DesignsPath)},,");
        WriteFleetCsv(table, "A", request.FleetA, catalog);
        WriteFleetCsv(table, "B", request.FleetB, catalog);
        Line(table, $"summary,A,{headline.WinsA.ToString(CultureInfo.InvariantCulture)},{Share(headline.WinsA, headline.Runs)},{Mean(headline.ShipsLostA, headline.Runs)},{Mean(headline.CostLostA, headline.Runs)},{Mean(headline.ShotsA, headline.Runs)},{Mean(headline.HitsA, headline.Runs)},");
        Line(table, $"summary,B,{headline.WinsB.ToString(CultureInfo.InvariantCulture)},{Share(headline.WinsB, headline.Runs)},{Mean(headline.ShipsLostB, headline.Runs)},{Mean(headline.CostLostB, headline.Runs)},{Mean(headline.ShotsB, headline.Runs)},{Mean(headline.HitsB, headline.Runs)},");
        Line(table, $"summary,draw,{headline.Draws.ToString(CultureInfo.InvariantCulture)},{Share(headline.Draws, headline.Runs)},,,,,");

        for (int cycles = 1; cycles < headline.ByCycles.Count; cycles++)
        {
            Line(table, $"duration,{cycles.ToString(CultureInfo.InvariantCulture)},{headline.ByCycles[cycles].ToString(CultureInfo.InvariantCulture)},,,,,,");
        }

        for (int i = 0; i < request.Matrix.Count; i++)
        {
            MatrixCell cell = request.Matrix[i];
            RunSet runs = cell.Runs;
            Line(
                table,
                $"matrix,{CsvText(cell.RowId)},{CsvText(cell.ColumnId)},{runs.WinsA.ToString(CultureInfo.InvariantCulture)},{runs.WinsB.ToString(CultureInfo.InvariantCulture)},{runs.Draws.ToString(CultureInfo.InvariantCulture)},{Share(runs.WinsA, runs.Runs)},{Mean(runs.CycleSum, runs.Runs)},");
        }

        for (int i = 0; i < headline.Modules.Count; i++)
        {
            ModuleScore score = headline.Modules[i];
            Line(
                table,
                $"module,{CsvText(score.Part)},{CsvText(score.Name)},{score.Copies.ToString(CultureInfo.InvariantCulture)},{Number(score.Cost)},{Number(score.MeanDamage)},{Number(score.DamagePerCost)},,");
        }

        Line(table, $"segment,{request.Sample.Milliseconds.ToString(NumberFormat, CultureInfo.InvariantCulture)},{request.Sample.AllocatedBytes.ToString(CultureInfo.InvariantCulture)},,,,,,");
        Line(table, $"opening,{request.Opening.Pairs.ToString(CultureInfo.InvariantCulture)},{request.Opening.Seen.ToString(CultureInfo.InvariantCulture)},{Number(request.Opening.MaxDistance)},{Number(request.Opening.MinReach)},{Number(request.Opening.MaxRangeA)},{Number(request.Opening.MaxRangeB)},,");
        return table.ToString();
    }

    private static void WriteFleetCsv(StringBuilder table, string side, FleetFile fleet, DesignCatalog catalog)
    {
        for (int i = 0; i < fleet.Groups.Count; i++)
        {
            FleetGroup group = fleet.Groups[i];
            ShipDesign design = catalog.Require(group.DesignId);
            Line(
                table,
                $"fleet,{side},{CsvText(design.Id)},{CsvText(design.Name)},{group.Count.ToString(CultureInfo.InvariantCulture)},{Number(design.Cost)},{Number(design.Cost * group.Count)},,");
        }
    }

    private static string Share(int part, int whole)
    {
        if (whole <= 0)
        {
            return 0.ToString(PercentFormat, CultureInfo.InvariantCulture);
        }

        return ((double)part / whole * PercentScale).ToString(PercentFormat, CultureInfo.InvariantCulture);
    }

    private static string Mean(double total, int runs)
    {
        if (runs <= 0)
        {
            return 0.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        return (total / runs).ToString(NumberFormat, CultureInfo.InvariantCulture);
    }

    private static string Number(double number)
    {
        if (!double.IsFinite(number))
        {
            return "";
        }

        return number.ToString(NumberFormat, CultureInfo.InvariantCulture);
    }

    private static string CsvText(string text)
    {
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n'))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }

    private static void Line(StringBuilder report, string text)
    {
        report.Append(text);
        report.Append('\n');
    }
}
