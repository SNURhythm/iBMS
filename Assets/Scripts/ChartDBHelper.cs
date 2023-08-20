using System;
using System.Collections.Generic;
using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;

public class ChartDBHelper
{
    public static ChartDBHelper Instance = new ChartDBHelper();


    private ChartDBHelper()
    {
        var connection = Connect();
        connection.Open();
        CreateTable(connection);
        connection.Close();
    }

    public SqliteConnection Connect()
    {
        return new SqliteConnection("URI=file:" + Path.Combine(Application.persistentDataPath, "chart.db"));
    }

    public void CreateTable(SqliteConnection connection)
    {
        const string q = @"CREATE TABLE IF NOT EXISTS chart_meta (
                        path       TEXT
                        primary key,
                        md5        TEXT not null,
                        sha256     TEXT not null,
                        title      TEXT,
                        subtitle   TEXT,
                        genre      TEXT,
                        artist     TEXT,
                        sub_artist  TEXT,
                        folder     TEXT,
                        stage_file  TEXT,
                        banner     TEXT,
                        back_bmp    TEXT,
                        preview    TEXT,
                        level      REAL,
                        difficulty INTEGER,
                        total     REAL,
                        bpm       REAL,
                        max_bpm     REAL,
                        min_bpm     REAL,
                        length     INTEGER,
                        rank      INTEGER,
                        player    INTEGER,
                        keys     INTEGER,
                        total_notes INTEGER,
                        total_long_notes INTEGER,
                        total_scratch_notes INTEGER,
                        total_backspin_notes INTEGER
                    )";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteReader();
    }

    public void Insert(SqliteConnection connection, ChartMeta chartMeta)
    {
        string q = @"REPLACE INTO chart_meta (
                        path,
                        md5,
                        sha256,
                        title,
                        subtitle,
                        genre,
                        artist,
                        sub_artist,
                        folder,
                        stage_file,
                        banner,
                        back_bmp,
                        preview,
                        level,
                        difficulty,
                        total,
                        bpm,
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys,
                        total_notes,
                        total_long_notes,
                        total_scratch_notes,
                        total_backspin_notes
                    ) VALUES (
                        @path,
                        @md5,
                        @sha256,
                        @title,
                        @subtitle,
                        @genre,
                        @artist,
                        @sub_artist,
                        @folder,
                        @stage_file,
                        @banner,
                        @back_bmp,
                        @preview,
                        @level,
                        @difficulty,
                        @total,
                        @bpm,
                        @max_bpm,
                        @min_bpm,
                        @length,
                        @rank,
                        @player,
                        @keys,
                        @total_notes,
                        @total_long_notes,
                        @total_scratch_notes,
                        @total_backspin_notes
                    )";

        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@path", chartMeta.BmsPath));
        command.Parameters.Add(new SqliteParameter("@md5", chartMeta.MD5));
        command.Parameters.Add(new SqliteParameter("@sha256", chartMeta.SHA256));
        command.Parameters.Add(new SqliteParameter("@title", chartMeta.Title));
        command.Parameters.Add(new SqliteParameter("@subtitle", chartMeta.SubTitle));
        command.Parameters.Add(new SqliteParameter("@genre", chartMeta.Genre));
        command.Parameters.Add(new SqliteParameter("@artist", chartMeta.Artist));
        command.Parameters.Add(new SqliteParameter("@sub_artist", chartMeta.SubArtist));
        command.Parameters.Add(new SqliteParameter("@folder", chartMeta.Folder));
        command.Parameters.Add(new SqliteParameter("@stage_file", chartMeta.StageFile));
        command.Parameters.Add(new SqliteParameter("@banner", chartMeta.Banner));
        command.Parameters.Add(new SqliteParameter("@back_bmp", chartMeta.BackBmp));
        command.Parameters.Add(new SqliteParameter("@preview", chartMeta.Preview));
        command.Parameters.Add(new SqliteParameter("@level", chartMeta.PlayLevel));
        command.Parameters.Add(new SqliteParameter("@difficulty", chartMeta.Difficulty));
        command.Parameters.Add(new SqliteParameter("@total", chartMeta.Total));
        command.Parameters.Add(new SqliteParameter("@bpm", chartMeta.Bpm));
        command.Parameters.Add(new SqliteParameter("@max_bpm", chartMeta.MaxBpm));
        command.Parameters.Add(new SqliteParameter("@min_bpm", chartMeta.MinBpm));
        command.Parameters.Add(new SqliteParameter("@length", chartMeta.PlayLength));
        command.Parameters.Add(new SqliteParameter("@rank", chartMeta.Rank));
        command.Parameters.Add(new SqliteParameter("@player", chartMeta.Player));
        command.Parameters.Add(new SqliteParameter("@keys", chartMeta.KeyMode));
        command.Parameters.Add(new SqliteParameter("@total_notes", chartMeta.TotalNotes));
        command.Parameters.Add(new SqliteParameter("@total_long_notes", chartMeta.TotalLongNotes));
        command.Parameters.Add(new SqliteParameter("@total_scratch_notes", chartMeta.TotalScratchNotes));
        command.Parameters.Add(new SqliteParameter("@total_backspin_notes", chartMeta.TotalBackSpinNotes));

        command.ExecuteNonQuery();
    }

    public List<ChartMeta> SelectAll(SqliteConnection connection)
    {
        const string q = @"SELECT
                        path,
                        md5,
                        sha256,
                        title,
                        subtitle,
                        genre,
                        artist,
                        sub_artist,
                        folder,
                        stage_file,
                        banner,
                        back_bmp,
                        preview,
                        level,
                        difficulty,
                        total,
                        bpm,
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys,
                        total_notes,
                        total_long_notes,
                        total_scratch_notes,
                        total_backspin_notes
                        FROM chart_meta";
        var command = connection.CreateCommand();
        command.CommandText = q;
        var reader = command.ExecuteReader();
        var chartMetas = new List<ChartMeta>();

        while (reader.Read())
        {
            try
            {
                var chartMeta = ReadChartMeta(reader);


                chartMetas.Add(chartMeta);
            }
            catch (Exception e)
            {
                Debug.LogError("Invalid chart data: " + e.Message);
            }
        }
        return chartMetas;
    }

    private ChartMeta ReadChartMeta(IDataReader reader)
    {
        var idx = 0;
        var chartMeta = new ChartMeta();
        chartMeta.BmsPath = reader.GetString(idx++);
        chartMeta.MD5 = reader.GetString(idx++);
        chartMeta.SHA256 = reader.GetString(idx++);
        chartMeta.Title = GetStringOrNull(reader, idx++);
        chartMeta.SubTitle = GetStringOrNull(reader, idx++);
        chartMeta.Genre = GetStringOrNull(reader, idx++);
        chartMeta.Artist = GetStringOrNull(reader, idx++);
        chartMeta.SubArtist = GetStringOrNull(reader, idx++);
        chartMeta.Folder = GetStringOrNull(reader, idx++);
        chartMeta.StageFile = GetStringOrNull(reader, idx++);
        chartMeta.Banner = GetStringOrNull(reader, idx++);
        chartMeta.BackBmp = GetStringOrNull(reader, idx++);
        chartMeta.Preview = GetStringOrNull(reader, idx++);
        chartMeta.PlayLevel = GetDoubleOrNull(reader, idx++);
        chartMeta.Difficulty = GetIntOrNull(reader, idx++);
        chartMeta.Total = GetDoubleOrNull(reader, idx++);
        chartMeta.Bpm = GetDoubleOrNull(reader, idx++);
        chartMeta.MaxBpm = GetDoubleOrNull(reader, idx++);
        chartMeta.MinBpm = GetDoubleOrNull(reader, idx++);
        chartMeta.PlayLength = GetLongOrNull(reader, idx++);
        chartMeta.Rank = GetIntOrNull(reader, idx++);
        chartMeta.Player = GetIntOrNull(reader, idx++);
        chartMeta.KeyMode = GetIntOrNull(reader, idx++);
        chartMeta.TotalNotes = GetIntOrNull(reader, idx++);
        chartMeta.TotalLongNotes = GetIntOrNull(reader, idx++);
        chartMeta.TotalScratchNotes = GetIntOrNull(reader, idx++);
        chartMeta.TotalBackSpinNotes = GetIntOrNull(reader, idx++);
        return chartMeta;
    }

    public List<ChartMeta> Search(SqliteConnection connection, string text)
    {
        const string q = @"SELECT path,
                        md5,
                        sha256,
                        title,
                        subtitle,
                        genre,
                        artist,
                        sub_artist,
                        folder,
                        stage_file,
                        banner,
                        back_bmp,
                        preview,
                        level,
                        difficulty,
                        total,
                        bpm,
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys,
                        total_notes,
                        total_long_notes,
                        total_scratch_notes,
                        total_backspin_notes
                        FROM chart_meta WHERE rtrim(title||' '||subtitle||' '||artist||' '||sub_artist||' '||genre) LIKE @text GROUP BY sha256";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@text", "%" + text + "%"));
        var reader = command.ExecuteReader();
        var chartMetas = new List<ChartMeta>();
        while (reader.Read())
        {
            try
            {
                var chartMeta = ReadChartMeta(reader);


                chartMetas.Add(chartMeta);
            }
            catch (Exception e)
            {
                Debug.LogError("Invalid chart data: " + e.Message);
            }
        }
        return chartMetas;
    }


    public void Clear(SqliteConnection connection)
    {
        const string q = @"DELETE FROM chart_meta";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteNonQuery();
    }

    public void Delete(SqliteConnection connection, string path)
    {
        const string q = @"DELETE FROM chart_meta WHERE path = @path";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@path", path));
        command.ExecuteNonQuery();
    }

    private static string GetStringOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }
        return reader.GetString(index);
    }

    private static int GetIntOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetInt32(index);
    }

    private static double GetDoubleOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetDouble(index);
    }

    private static float GetFloatOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetFloat(index);
    }

    private static long GetLongOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetInt64(index);
    }
}
