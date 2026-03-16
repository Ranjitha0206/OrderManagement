using System.Runtime.InteropServices;
using System.Text;

namespace OrdersManagement.Data;

/// <summary>
/// Lightweight SQLite wrapper using P/Invoke.
/// Automatically selects the correct native library for Windows, Linux, and macOS.
/// </summary>
public sealed class SqliteConnection : IDisposable
{
    // Detect OS at runtime and pick the right SQLite library name
    private static readonly string LibName =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "winsqlite3" :
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX)     ? "libsqlite3.dylib" :
                                                               "libsqlite3.so.0";

    private IntPtr _db = IntPtr.Zero;
    private bool _disposed;

    public SqliteConnection(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public void Open()
    {
        var path = ParsePath(ConnectionString);
        int result = NativeMethods.Open(LibName, path, out _db);
        if (result != 0)
            throw new InvalidOperationException($"Failed to open SQLite database (code {result})");
    }

    public void Close()
    {
        if (_db != IntPtr.Zero)
        {
            NativeMethods.Close(LibName, _db);
            _db = IntPtr.Zero;
        }
    }

    public SqliteCommand CreateCommand()
    {
        EnsureOpen();
        return new SqliteCommand(_db, LibName);
    }

    public int Execute(string sql, Dictionary<string, object?>? parameters = null)
    {
        using var cmd = CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters[p.Key] = p.Value;
        return cmd.ExecuteNonQuery();
    }

    public List<Dictionary<string, object?>> Query(string sql, Dictionary<string, object?>? parameters = null)
    {
        using var cmd = CreateCommand();
        cmd.CommandText = sql;
        if (parameters != null)
            foreach (var p in parameters)
                cmd.Parameters[p.Key] = p.Value;
        return cmd.ExecuteReader();
    }

    private void EnsureOpen()
    {
        if (_db == IntPtr.Zero) Open();
    }

    private static string ParsePath(string cs)
    {
        if (cs.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            return cs["Data Source=".Length..];
        return cs;
    }

    public void Dispose()
    {
        if (!_disposed) { Close(); _disposed = true; }
    }
}

public sealed class SqliteCommand : IDisposable
{
    private readonly IntPtr _db;
    private readonly string _lib;

    internal SqliteCommand(IntPtr db, string lib)
    {
        _db = db;
        _lib = lib;
        Parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public string CommandText { get; set; } = string.Empty;
    public Dictionary<string, object?> Parameters { get; }

    public int ExecuteNonQuery()
    {
        var stmt = Prepare();
        try
        {
            BindParameters(stmt);
            int result = NativeMethods.Step(_lib, stmt);
            if (result != NativeMethods.SQLITE_DONE && result != NativeMethods.SQLITE_ROW)
                ThrowError(result);
            return NativeMethods.Changes(_lib, _db);
        }
        finally { NativeMethods.Finalize(_lib, stmt); }
    }

    public List<Dictionary<string, object?>> ExecuteReader()
    {
        var rows = new List<Dictionary<string, object?>>();
        var stmt = Prepare();
        try
        {
            BindParameters(stmt);
            while (NativeMethods.Step(_lib, stmt) == NativeMethods.SQLITE_ROW)
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                int cols = NativeMethods.ColumnCount(_lib, stmt);
                for (int i = 0; i < cols; i++)
                {
                    var name = Marshal.PtrToStringAnsi(NativeMethods.ColumnName(_lib, stmt, i)) ?? $"col{i}";
                    row[name] = ReadColumnValue(stmt, i);
                }
                rows.Add(row);
            }
        }
        finally { NativeMethods.Finalize(_lib, stmt); }
        return rows;
    }

    public long LastInsertRowId() => NativeMethods.LastInsertRowId(_lib, _db);

    private IntPtr Prepare()
    {
        var sql = Encoding.UTF8.GetBytes(CommandText + "\0");
        int result = NativeMethods.Prepare(_lib, _db, sql, sql.Length, out var stmt, IntPtr.Zero);
        if (result != NativeMethods.SQLITE_OK) ThrowError(result);
        return stmt;
    }

    private void BindParameters(IntPtr stmt)
    {
        foreach (var (key, value) in Parameters)
        {
            var name = key.StartsWith('@') ? key : "@" + key;
            int idx = NativeMethods.BindParameterIndex(_lib, stmt, name);
            if (idx == 0) continue;

            if (value is null)
                NativeMethods.BindNull(_lib, stmt, idx);
            else if (value is int i)
                NativeMethods.BindInt(_lib, stmt, idx, i);
            else if (value is long l)
                NativeMethods.BindInt64(_lib, stmt, idx, l);
            else if (value is double d)
                NativeMethods.BindDouble(_lib, stmt, idx, d);
            else if (value is decimal dec)
                NativeMethods.BindDouble(_lib, stmt, idx, (double)dec);
            else
            {
                var bytes = Encoding.UTF8.GetBytes(value.ToString()! + "\0");
                NativeMethods.BindText(_lib, stmt, idx, bytes, bytes.Length - 1, new IntPtr(-1));
            }
        }
    }

    private object? ReadColumnValue(IntPtr stmt, int col)
    {
        int type = NativeMethods.ColumnType(_lib, stmt, col);
        return type switch
        {
            1 => (object)NativeMethods.ColumnInt64(_lib, stmt, col),
            2 => NativeMethods.ColumnDouble(_lib, stmt, col),
            3 => Marshal.PtrToStringUTF8(NativeMethods.ColumnText(_lib, stmt, col)),
            5 => null,
            _ => Marshal.PtrToStringUTF8(NativeMethods.ColumnText(_lib, stmt, col))
        };
    }

    private void ThrowError(int code)
    {
        var msg = Marshal.PtrToStringAnsi(NativeMethods.Errmsg(_lib, _db)) ?? "Unknown SQLite error";
        throw new InvalidOperationException($"SQLite error {code}: {msg}");
    }

    public void Dispose() { }
}

/// <summary>
/// Dynamic P/Invoke dispatcher — routes calls to the correct native library at runtime.
/// </summary>
internal static class NativeMethods
{
    public const int SQLITE_OK   = 0;
    public const int SQLITE_ROW  = 100;
    public const int SQLITE_DONE = 101;

    // Windows
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_open")]           private static extern int  w_open(string f, out IntPtr db);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_close")]          private static extern int  w_close(IntPtr db);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_prepare_v2")]     private static extern int  w_prepare(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr tail);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_step")]           private static extern int  w_step(IntPtr stmt);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_finalize")]       private static extern int  w_finalize(IntPtr stmt);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_changes")]        private static extern int  w_changes(IntPtr db);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_last_insert_rowid")] private static extern long w_lastrow(IntPtr db);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_count")]   private static extern int  w_colcount(IntPtr stmt);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_name")]    private static extern IntPtr w_colname(IntPtr stmt, int col);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_type")]    private static extern int  w_coltype(IntPtr stmt, int col);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_int64")]   private static extern long w_colint64(IntPtr stmt, int col);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_double")]  private static extern double w_coldbl(IntPtr stmt, int col);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_column_text")]    private static extern IntPtr w_coltext(IntPtr stmt, int col);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_parameter_index")] private static extern int w_bindidx(IntPtr stmt, string name);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_null")]      private static extern int  w_bindnull(IntPtr stmt, int i);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_int")]       private static extern int  w_bindint(IntPtr stmt, int i, int v);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_int64")]     private static extern int  w_bindint64(IntPtr stmt, int i, long v);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_double")]    private static extern int  w_binddbl(IntPtr stmt, int i, double v);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_bind_text")]      private static extern int  w_bindtext(IntPtr stmt, int i, byte[] v, int n, IntPtr d);
    [DllImport("winsqlite3",      EntryPoint = "sqlite3_errmsg")]         private static extern IntPtr w_errmsg(IntPtr db);

    // Linux
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_open")]           private static extern int  l_open(string f, out IntPtr db);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_close")]          private static extern int  l_close(IntPtr db);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_prepare_v2")]     private static extern int  l_prepare(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr tail);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_step")]           private static extern int  l_step(IntPtr stmt);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_finalize")]       private static extern int  l_finalize(IntPtr stmt);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_changes")]        private static extern int  l_changes(IntPtr db);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_last_insert_rowid")] private static extern long l_lastrow(IntPtr db);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_count")]   private static extern int  l_colcount(IntPtr stmt);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_name")]    private static extern IntPtr l_colname(IntPtr stmt, int col);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_type")]    private static extern int  l_coltype(IntPtr stmt, int col);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_int64")]   private static extern long l_colint64(IntPtr stmt, int col);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_double")]  private static extern double l_coldbl(IntPtr stmt, int col);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_column_text")]    private static extern IntPtr l_coltext(IntPtr stmt, int col);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_parameter_index")] private static extern int l_bindidx(IntPtr stmt, string name);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_null")]      private static extern int  l_bindnull(IntPtr stmt, int i);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_int")]       private static extern int  l_bindint(IntPtr stmt, int i, int v);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_int64")]     private static extern int  l_bindint64(IntPtr stmt, int i, long v);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_double")]    private static extern int  l_binddbl(IntPtr stmt, int i, double v);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_bind_text")]      private static extern int  l_bindtext(IntPtr stmt, int i, byte[] v, int n, IntPtr d);
    [DllImport("libsqlite3.so.0", EntryPoint = "sqlite3_errmsg")]         private static extern IntPtr l_errmsg(IntPtr db);

    // macOS
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_open")]          private static extern int  m_open(string f, out IntPtr db);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_close")]         private static extern int  m_close(IntPtr db);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_prepare_v2")]    private static extern int  m_prepare(IntPtr db, byte[] sql, int n, out IntPtr stmt, IntPtr tail);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_step")]          private static extern int  m_step(IntPtr stmt);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_finalize")]      private static extern int  m_finalize(IntPtr stmt);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_changes")]       private static extern int  m_changes(IntPtr db);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_last_insert_rowid")] private static extern long m_lastrow(IntPtr db);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_count")]  private static extern int  m_colcount(IntPtr stmt);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_name")]   private static extern IntPtr m_colname(IntPtr stmt, int col);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_type")]   private static extern int  m_coltype(IntPtr stmt, int col);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_int64")]  private static extern long m_colint64(IntPtr stmt, int col);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_double")] private static extern double m_coldbl(IntPtr stmt, int col);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_column_text")]   private static extern IntPtr m_coltext(IntPtr stmt, int col);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_parameter_index")] private static extern int m_bindidx(IntPtr stmt, string name);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_null")]     private static extern int  m_bindnull(IntPtr stmt, int i);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_int")]      private static extern int  m_bindint(IntPtr stmt, int i, int v);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_int64")]    private static extern int  m_bindint64(IntPtr stmt, int i, long v);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_double")]   private static extern int  m_binddbl(IntPtr stmt, int i, double v);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_bind_text")]     private static extern int  m_bindtext(IntPtr stmt, int i, byte[] v, int n, IntPtr d);
    [DllImport("libsqlite3.dylib", EntryPoint = "sqlite3_errmsg")]        private static extern IntPtr m_errmsg(IntPtr db);

    // ── Dispatcher methods ────────────────────────────────────────────────
    private static bool IsWin => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static int    Open(string lib, string f, out IntPtr db)                        { if (IsWin) return w_open(f, out db); if (IsMac) return m_open(f, out db); return l_open(f, out db); }
    public static int    Close(string lib, IntPtr db)                                     { if (IsWin) return w_close(db);       if (IsMac) return m_close(db);       return l_close(db); }
    public static int    Prepare(string lib, IntPtr db, byte[] sql, int n, out IntPtr s, IntPtr t) { if (IsWin) return w_prepare(db,sql,n,out s,t); if (IsMac) return m_prepare(db,sql,n,out s,t); return l_prepare(db,sql,n,out s,t); }
    public static int    Step(string lib, IntPtr s)                                       { if (IsWin) return w_step(s);         if (IsMac) return m_step(s);         return l_step(s); }
    public static int    Finalize(string lib, IntPtr s)                                   { if (IsWin) return w_finalize(s);     if (IsMac) return m_finalize(s);     return l_finalize(s); }
    public static int    Changes(string lib, IntPtr db)                                   { if (IsWin) return w_changes(db);     if (IsMac) return m_changes(db);     return l_changes(db); }
    public static long   LastInsertRowId(string lib, IntPtr db)                           { if (IsWin) return w_lastrow(db);     if (IsMac) return m_lastrow(db);     return l_lastrow(db); }
    public static int    ColumnCount(string lib, IntPtr s)                                { if (IsWin) return w_colcount(s);     if (IsMac) return m_colcount(s);     return l_colcount(s); }
    public static IntPtr ColumnName(string lib, IntPtr s, int c)                          { if (IsWin) return w_colname(s,c);    if (IsMac) return m_colname(s,c);    return l_colname(s,c); }
    public static int    ColumnType(string lib, IntPtr s, int c)                          { if (IsWin) return w_coltype(s,c);    if (IsMac) return m_coltype(s,c);    return l_coltype(s,c); }
    public static long   ColumnInt64(string lib, IntPtr s, int c)                         { if (IsWin) return w_colint64(s,c);   if (IsMac) return m_colint64(s,c);   return l_colint64(s,c); }
    public static double ColumnDouble(string lib, IntPtr s, int c)                        { if (IsWin) return w_coldbl(s,c);     if (IsMac) return m_coldbl(s,c);     return l_coldbl(s,c); }
    public static IntPtr ColumnText(string lib, IntPtr s, int c)                          { if (IsWin) return w_coltext(s,c);    if (IsMac) return m_coltext(s,c);    return l_coltext(s,c); }
    public static int    BindParameterIndex(string lib, IntPtr s, string n)               { if (IsWin) return w_bindidx(s,n);    if (IsMac) return m_bindidx(s,n);    return l_bindidx(s,n); }
    public static int    BindNull(string lib, IntPtr s, int i)                            { if (IsWin) return w_bindnull(s,i);   if (IsMac) return m_bindnull(s,i);   return l_bindnull(s,i); }
    public static int    BindInt(string lib, IntPtr s, int i, int v)                      { if (IsWin) return w_bindint(s,i,v);  if (IsMac) return m_bindint(s,i,v);  return l_bindint(s,i,v); }
    public static int    BindInt64(string lib, IntPtr s, int i, long v)                   { if (IsWin) return w_bindint64(s,i,v);if (IsMac) return m_bindint64(s,i,v);return l_bindint64(s,i,v); }
    public static int    BindDouble(string lib, IntPtr s, int i, double v)                { if (IsWin) return w_binddbl(s,i,v);  if (IsMac) return m_binddbl(s,i,v);  return l_binddbl(s,i,v); }
    public static int    BindText(string lib, IntPtr s, int i, byte[] v, int n, IntPtr d) { if (IsWin) return w_bindtext(s,i,v,n,d); if (IsMac) return m_bindtext(s,i,v,n,d); return l_bindtext(s,i,v,n,d); }
    public static IntPtr Errmsg(string lib, IntPtr db)                                    { if (IsWin) return w_errmsg(db);      if (IsMac) return m_errmsg(db);      return l_errmsg(db); }
}
