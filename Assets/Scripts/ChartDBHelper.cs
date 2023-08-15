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
        const string q = @"CREATE TABLE IF NOT EXISTS charts (
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

    public void Insert(Chart chart)
    {
        const string q = @"INSERT INTO charts (
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
        command.Parameters.Add(new SqliteParameter("@path", chart.BmsPath));
        command.Parameters.Add(new SqliteParameter("@md5", chart.MD5));
        command.Parameters.Add(new SqliteParameter("@sha256", chart.SHA256));
        command.Parameters.Add(new SqliteParameter("@title", chart.Title));
        command.Parameters.Add(new SqliteParameter("@subtitle", chart.SubTitle));
        command.Parameters.Add(new SqliteParameter("@genre", chart.Genre));
        command.Parameters.Add(new SqliteParameter("@artist", chart.Artist));
        command.Parameters.Add(new SqliteParameter("@sub_artist", chart.SubArtist));
        command.Parameters.Add(new SqliteParameter("@folder", chart.Folder));
        command.Parameters.Add(new SqliteParameter("@stage_file", chart.StageFile));
        command.Parameters.Add(new SqliteParameter("@banner", chart.Banner));
        command.Parameters.Add(new SqliteParameter("@back_bmp", chart.BackBmp));
        command.Parameters.Add(new SqliteParameter("@preview", chart.Preview));
        command.Parameters.Add(new SqliteParameter("@level", chart.PlayLevel));
        command.Parameters.Add(new SqliteParameter("@difficulty", chart.Difficulty));
        command.Parameters.Add(new SqliteParameter("@max_bpm", chart.MaxBpm));
        command.Parameters.Add(new SqliteParameter("@min_bpm", chart.MinBpm));
        command.Parameters.Add(new SqliteParameter("@length", chart.PlayLength));
        command.Parameters.Add(new SqliteParameter("@rank", chart.Rank));

        command.ExecuteNonQuery();
    }
    
    public Chart Select(string path)
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
                        rank FROM charts WHERE path = @path";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@path", path));
        var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }
        var chart = new Chart
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
        return chart;
    }
    
    public List<Chart> SelectAll()
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
                        rank FROM charts";
        var command = connection.CreateCommand();
        command.CommandText = q;
        var reader = command.ExecuteReader();
        var charts = new List<Chart>();

        while (reader.Read())
        {
            try
            {
                var chart = new Chart();
                chart.BmsPath = reader.GetString(0);
                chart.MD5 = reader.GetString(1);
                chart.SHA256 = reader.GetString(2);
                chart.Title = GetStringOrNull(reader, 3);
                chart.SubTitle = GetStringOrNull(reader, 4);
                chart.Genre = GetStringOrNull(reader, 5);
                chart.Artist = GetStringOrNull(reader, 6);
                chart.SubArtist = GetStringOrNull(reader, 7);
                chart.Folder = GetStringOrNull(reader, 8);
                chart.StageFile = GetStringOrNull(reader, 9);
                chart.Banner = GetStringOrNull(reader, 10);
                chart.BackBmp = GetStringOrNull(reader, 11);
                chart.Preview = GetStringOrNull(reader, 12);
                chart.PlayLevel = GetIntOrNull(reader, 13);
                chart.Difficulty = GetIntOrNull(reader, 14);
                chart.MaxBpm = GetDoubleOrNull(reader, 15);
                chart.MinBpm = GetDoubleOrNull(reader, 16);
                chart.PlayLength = GetLongOrNull(reader, 17);
                chart.Rank = GetIntOrNull(reader, 18);

                charts.Add(chart);
            }
            catch (Exception e)
            {
                Debug.LogError("Invalid chart data: " + e.Message);
            }
        }
        return charts;
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
        const string q = @"DELETE FROM charts";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.ExecuteNonQuery();
    }

    public void Delete(string path)
    {
        const string q = @"DELETE FROM charts WHERE path = @path";
        var command = connection.CreateCommand();
        command.CommandText = q;
        command.Parameters.Add(new SqliteParameter("@path", path));
        command.ExecuteNonQuery();
    }
}
