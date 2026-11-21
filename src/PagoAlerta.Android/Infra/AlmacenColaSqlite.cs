using Android.Database;
using Android.Database.Sqlite;
using PagoAlerta.Core.Cola;

namespace PagoAlerta.Movil.Infra;

/// <summary>
/// HT-09: cola local persistente en SQLite (la base de datos incluida en Android, sin librerias extra).
/// Sobrevive a cierres de la app, reinicios del celular y falta de internet.
/// </summary>
public class AlmacenColaSqlite : SQLiteOpenHelper, IAlmacenCola
{
    private const string Tabla = "cola";
    private static readonly object Candado = new();

    public AlmacenColaSqlite(Context ctx) : base(ctx, "cola_envio.db", null, 1) { }

    public override void OnCreate(SQLiteDatabase? db) =>
        db!.ExecSQL($"CREATE TABLE {Tabla} (" +
                    "id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "hash TEXT NOT NULL UNIQUE, " +
                    "json TEXT NOT NULL, " +
                    "intentos INTEGER NOT NULL DEFAULT 0, " +
                    "proximo INTEGER NOT NULL DEFAULT 0, " +
                    "creado INTEGER NOT NULL)");

    public override void OnUpgrade(SQLiteDatabase? db, int oldVersion, int newVersion) { }

    public bool Encolar(string hash, string json, long ahoraMs)
    {
        lock (Candado)
        {
            using var valores = new ContentValues();
            valores.Put("hash", hash);
            valores.Put("json", json);
            valores.Put("intentos", 0);
            valores.Put("proximo", 0L);
            valores.Put("creado", ahoraMs);
            // IGNORE: si el hash ya esta en la cola, no se duplica (devuelve -1).
            return WritableDatabase!.InsertWithOnConflict(Tabla, null, valores, Conflict.Ignore) != -1;
        }
    }

    public ItemCola? Siguiente(long ahoraMs, bool ignorarEspera)
    {
        lock (Candado)
        {
            var sql = $"SELECT id, hash, json, intentos, proximo, creado FROM {Tabla} " +
                      (ignorarEspera ? "" : "WHERE proximo <= ? ") + "ORDER BY id ASC LIMIT 1";
            using var c = ReadableDatabase!.RawQuery(sql, ignorarEspera ? null : new[] { ahoraMs.ToString() });
            if (c is null || !c.MoveToFirst()) return null;
            return new ItemCola(c.GetLong(0), c.GetString(1)!, c.GetString(2)!, c.GetInt(3), c.GetLong(4), c.GetLong(5));
        }
    }

    public void Eliminar(long id)
    {
        lock (Candado) WritableDatabase!.Delete(Tabla, "id = ?", new[] { id.ToString() });
    }

    public void Reprogramar(long id, int intentos, long proximoIntentoMs)
    {
        lock (Candado)
        {
            using var valores = new ContentValues();
            valores.Put("intentos", intentos);
            valores.Put("proximo", proximoIntentoMs);
            WritableDatabase!.Update(Tabla, valores, "id = ?", new[] { id.ToString() });
        }
    }

    public int Contar()
    {
        lock (Candado) return (int)DatabaseUtils.QueryNumEntries(ReadableDatabase, Tabla);
    }
}
