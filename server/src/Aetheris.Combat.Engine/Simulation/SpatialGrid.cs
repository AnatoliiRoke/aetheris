namespace Aetheris.Combat.Engine;

/// <summary>
/// A uniform grid. A query reads the cells the circle touches and writes their indices
/// into a buffer the caller owns.
/// </summary>
public sealed class SpatialGrid
{
    private int[] _cellStart = [];
    private int[] _cellFill = [];
    private int[] _indices = [];
    private double[] _x = [];
    private double[] _y = [];
    private int _columns = 1;
    private int _rows = 1;
    private int _count;
    private double _cellSize = 1;

    /// <summary>
    /// Rebuilds the cells from the positions. The arrays are read only for <paramref name="count"/> entries.
    /// </summary>
    /// <param name="count">How many positions are live.</param>
    /// <param name="xs">The first axis.</param>
    /// <param name="ys">The second axis.</param>
    /// <param name="width">The width of the field.</param>
    /// <param name="height">The height of the field.</param>
    /// <param name="cellSize">The side of one cell. A non-positive size makes one cell.</param>
    public void Rebuild(int count, double[] xs, double[] ys, double width, double height, double cellSize)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);

        if (count < 0)
        {
            count = 0;
        }

        _count = count;
        _cellSize = cellSize > 0 ? cellSize : (width > 0 ? width : 1);
        _columns = Columns(width, _cellSize);
        _rows = Columns(height, _cellSize);
        int cells = _columns * _rows;

        if (_cellStart.Length < cells)
        {
            _cellStart = new int[cells];
            _cellFill = new int[cells];
        }

        if (_indices.Length < count)
        {
            _indices = new int[count];
        }

        if (_x.Length < count)
        {
            _x = new double[count];
            _y = new double[count];
        }

        Array.Clear(_cellFill, 0, cells);

        for (int i = 0; i < count; i++)
        {
            _x[i] = xs[i];
            _y[i] = ys[i];
            _cellFill[Cell(xs[i], ys[i])]++;
        }

        int cursor = 0;

        for (int cell = 0; cell < cells; cell++)
        {
            _cellStart[cell] = cursor;
            cursor += _cellFill[cell];
            _cellFill[cell] = 0;
        }

        for (int i = 0; i < count; i++)
        {
            int cell = Cell(_x[i], _y[i]);
            _indices[_cellStart[cell] + _cellFill[cell]] = i;
            _cellFill[cell]++;
        }
    }

    /// <summary>
    /// Writes the indices of the positions whose cells meet the circle.
    /// </summary>
    /// <param name="x">The first axis of the query.</param>
    /// <param name="y">The second axis of the query.</param>
    /// <param name="radius">The radius.</param>
    /// <param name="into">The buffer. It has to be at least as long as the number of positions.</param>
    /// <returns>How many indices were written.</returns>
    public int Collect(double x, double y, double radius, int[] into)
    {
        ArgumentNullException.ThrowIfNull(into);

        if (_count == 0 || radius < 0)
        {
            return 0;
        }

        int minColumn = ColumnOf(x - radius, _columns);
        int maxColumn = ColumnOf(x + radius, _columns);
        int minRow = RowOf(y - radius, _rows);
        int maxRow = RowOf(y + radius, _rows);
        int written = 0;

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minColumn; column <= maxColumn; column++)
            {
                int cell = (row * _columns) + column;
                int start = _cellStart[cell];
                int end = start + _cellFill[cell];

                for (int index = start; index < end; index++)
                {
                    if (written < into.Length)
                    {
                        into[written] = _indices[index];
                        written++;
                    }
                }
            }
        }

        return written;
    }

    private int Cell(double x, double y) => (RowOf(y, _rows) * _columns) + ColumnOf(x, _columns);

    private int ColumnOf(double x, int columns)
    {
        if (_cellSize <= 0)
        {
            return 0;
        }

        int column = (int)(x / _cellSize);

        if (column < 0)
        {
            return 0;
        }

        if (column >= columns)
        {
            return columns - 1;
        }

        return column;
    }

    private int RowOf(double y, int rows) => ColumnOf(y, rows);

    private static int Columns(double span, double cellSize)
    {
        if (span <= 0 || cellSize <= 0)
        {
            return 1;
        }

        int columns = (int)(span / cellSize);

        if ((columns * cellSize) < span)
        {
            columns++;
        }

        return columns > 0 ? columns : 1;
    }
}
