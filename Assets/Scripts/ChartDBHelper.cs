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
        
    }
    
    public void Open()
    {
        connection = new SqliteConnection("URI=file:" + Path.Combine(Application.persistentDataPath, "chart.db"));
        connection.Open();
        CreateTable();
    }
    
    public void Close()
    {
        connection.Close();
        connection = null;
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
                        level      INTEGER,
                        difficulty INTEGER,
                        max_bpm     REAL,
                        min_bpm     REAL,
                        length     INTEGER,
                        rank      INTEGER,
                        date       INTEGER,
                        favorite   INTEGER,
                        add_date    INTEGER
                    )";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteReader();
    }

    public void Insert(ChartMeta chartMeta)
    {
        const string q = @"INSERT INTO chart_meta (
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
                        rank
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
                        @rank
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

        command.ExecuteNonQuery();
    }
    
    public ChartMeta Select(string path)
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
                        rank FROM chart_meta WHERE path = @path";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@path", path));
        var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var chartMeta = new ChartMeta
        {
            BmsPath = reader.GetString(0),
            MD5 = reader.GetString(1),
            SHA256 = reader.GetString(2),
            Title = reader.GetString(3),
            SubTitle = reader.GetString(4),
            Genre = reader.GetString(5),
            Artist = reader.GetString(6),
            SubArtist = reader.GetString(7),
            Folder = reader.GetString(8),
            StageFile = reader.GetString(9),
            Banner = reader.GetString(10),
            BackBmp = reader.GetString(11),
            Preview = reader.GetString(12),
            PlayLevel = reader.GetInt32(13),
            Difficulty = reader.GetInt32(14),
            MaxBpm = reader.GetDouble(15),
            MinBpm = reader.GetDouble(16),
            PlayLength = reader.GetInt64(17),
            Rank = reader.GetInt32(18)
        };
        return chartMeta;
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
                        rank FROM chart_meta";
        var command = connection.CreateCommand();
        command.CommandText = q;
        var reader = command.ExecuteReader();
        var chartMetas = new List<ChartMeta>();

        while (reader.Read())
        {
            try
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
                chartMeta.PlayLevel = GetIntOrNull(reader, 13);
                chartMeta.Difficulty = GetIntOrNull(reader, 14);
                chartMeta.MaxBpm = GetDoubleOrNull(reader, 15);
                chartMeta.MinBpm = GetDoubleOrNull(reader, 16);
                chartMeta.PlayLength = GetLongOrNull(reader, 17);
                chartMeta.Rank = GetIntOrNull(reader, 18);

                chartMetas.Add(chartMeta);
            }
            catch (Exception e)
            {
                Debug.LogError("Invalid chart data: " + e.Message);
            }
        }
        return chartMetas;
    }
    
    private string GetStringOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }
        return reader.GetString(index);
    }
    
    private int GetIntOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetInt32(index);
    }
    
    private double GetDoubleOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetDouble(index);
    }
    
    private long GetLongOrNull(IDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return 0;
        }
        return reader.GetInt64(index);
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
}
