using System;
using System.Collections.Generic;
using UnityEngine;
using System.Data;
using Mono.Data.Sqlite;
using System.IO;

public class ChartDBHelper
{
    public static ChartDBHelper Instance = new ChartDBHelper();
    

    private IDbConnection connection;
    private ChartDBHelper()
    {
        connection = new SqliteConnection("URI=file:" + Path.Combine(Application.persistentDataPath, "chart.db"));
        connection.Open();
        CreateTable();
    }
    
    public void CreateTable()
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
                        max_bpm     REAL,
                        min_bpm     REAL,
                        length     INTEGER,
                        rank      INTEGER,
                        player    INTEGER,
                        keys     INTEGER
                    )";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteReader();
    }

    public void Insert(ChartMeta chartMeta)
    {
        string q = @"INSERT INTO chart_meta (
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
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys
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
                        @max_bpm,
                        @min_bpm,
                        @length,
                        @rank,
                        @player,
                        @keys
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
        command.Parameters.Add(new SqliteParameter("@max_bpm", chartMeta.MaxBpm));
        command.Parameters.Add(new SqliteParameter("@min_bpm", chartMeta.MinBpm));
        command.Parameters.Add(new SqliteParameter("@length", chartMeta.PlayLength));
        command.Parameters.Add(new SqliteParameter("@rank", chartMeta.Rank));
        command.Parameters.Add(new SqliteParameter("@player", chartMeta.Player));
        command.Parameters.Add(new SqliteParameter("@keys", chartMeta.KeyMode));

        command.ExecuteNonQuery();
    }
    
    public List<ChartMeta> SelectAll()
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
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys
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
        var chartMeta = new ChartMeta();
        chartMeta.BmsPath = reader.GetString(0);
        chartMeta.MD5 = reader.GetString(1);
        chartMeta.SHA256 = reader.GetString(2);
        chartMeta.Title = GetStringOrNull(reader, 3);
        chartMeta.SubTitle = GetStringOrNull(reader, 4);
        chartMeta.Genre = GetStringOrNull(reader, 5);
        chartMeta.Artist = GetStringOrNull(reader, 6);
        chartMeta.SubArtist = GetStringOrNull(reader, 7);
        chartMeta.Folder = GetStringOrNull(reader, 8);
        chartMeta.StageFile = GetStringOrNull(reader, 9);
        chartMeta.Banner = GetStringOrNull(reader, 10);
        chartMeta.BackBmp = GetStringOrNull(reader, 11);
        chartMeta.Preview = GetStringOrNull(reader, 12);
        chartMeta.PlayLevel = GetDoubleOrNull(reader, 13);
        chartMeta.Difficulty = GetIntOrNull(reader, 14);
        chartMeta.MaxBpm = GetDoubleOrNull(reader, 15);
        chartMeta.MinBpm = GetDoubleOrNull(reader, 16);
        chartMeta.PlayLength = GetLongOrNull(reader, 17);
        chartMeta.Rank = GetIntOrNull(reader, 18);
        chartMeta.Player = GetIntOrNull(reader, 19);
        chartMeta.KeyMode = GetIntOrNull(reader, 20);
        return chartMeta;
    }

    public List<ChartMeta> Search(string text)
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
                        max_bpm,
                        min_bpm,
                        length,
                        rank,
                        player,
                        keys
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
   

    public void Clear()
    {
        const string q = @"DELETE FROM chart_meta";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteNonQuery();
    }

    public void Delete(string path)
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
