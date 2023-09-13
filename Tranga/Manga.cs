﻿using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using static System.IO.UnixFileMode;

namespace Tranga;

/// <summary>
/// Contains information on a Publication (Manga)
/// </summary>
public struct Manga
{
    public string sortName { get; }
    public List<string> authors { get; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Dictionary<string,string> altTitles { get; }
    // ReSharper disable once MemberCanBePrivate.Global
    public string? description { get; }
    public string[] tags { get; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string? coverUrl { get; }
    public string? coverFileNameInCache { get; set; }
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Dictionary<string,string> links { get; }
    // ReSharper disable once MemberCanBePrivate.Global
    public int? year { get; }
    public string? originalLanguage { get; }
    // ReSharper disable once MemberCanBePrivate.Global
    public string status { get; }
    public string folderName { get; private set; }
    public string publicationId { get; }
    public string internalId { get; }
    public float ignoreChaptersBelow { get; set; }

    private static readonly Regex LegalCharacters = new (@"[A-Z]*[a-z]*[0-9]* *\.*-*,*'*\'*\)*\(*~*!*");

    [JsonConstructor]
    public Manga(string sortName, List<string> authors, string? description, Dictionary<string,string> altTitles, string[] tags, string? coverUrl, string? coverFileNameInCache, Dictionary<string,string>? links, int? year, string? originalLanguage, string status, string publicationId, string? folderName = null, float? ignoreChaptersBelow = 0)
    {
        this.sortName = sortName;
        this.authors = authors;
        this.description = description;
        this.altTitles = altTitles;
        this.tags = tags;
        this.coverFileNameInCache = coverFileNameInCache;
        this.coverUrl = coverUrl;
        this.links = links ?? new Dictionary<string, string>();
        this.year = year;
        this.originalLanguage = originalLanguage;
        this.status = status;
        this.publicationId = publicationId;
        this.folderName = folderName ?? string.Concat(LegalCharacters.Matches(sortName));
        while (this.folderName.EndsWith('.'))
            this.folderName = this.folderName.Substring(0, this.folderName.Length - 1);
        string onlyLowerLetters = string.Concat(this.sortName.ToLower().Where(Char.IsLetter));
        this.internalId = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{onlyLowerLetters}{this.year}"));
        this.ignoreChaptersBelow = ignoreChaptersBelow ?? 0f;
    }

    public override string ToString()
    {
        return $"Publication {sortName} {internalId}";
    }

    public string CreatePublicationFolder(string downloadDirectory)
    {
        string publicationFolder = Path.Join(downloadDirectory, this.folderName);
        if(!Directory.Exists(publicationFolder))
            Directory.CreateDirectory(publicationFolder);
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            File.SetUnixFileMode(publicationFolder, GroupRead | GroupWrite | GroupExecute | OtherRead | OtherWrite | OtherExecute | UserRead | UserWrite | UserExecute);
        return publicationFolder;
    }

    public void MovePublicationFolder(string downloadDirectory, string newFolderName)
    {
        string oldPath = Path.Join(downloadDirectory, this.folderName);
        this.folderName = newFolderName;
        string newPath = CreatePublicationFolder(downloadDirectory);
        if(Directory.Exists(oldPath))
            Directory.Move(oldPath, newPath);
    }

    public void SaveSeriesInfoJson(string downloadDirectory)
    {
        string publicationFolder = CreatePublicationFolder(downloadDirectory);
        string seriesInfoPath = Path.Join(publicationFolder, "series.json");
        if(!File.Exists(seriesInfoPath))
            File.WriteAllText(seriesInfoPath,this.GetSeriesInfoJson());
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            File.SetUnixFileMode(seriesInfoPath, GroupRead | GroupWrite | OtherRead | OtherWrite | UserRead | UserWrite);
    }
    
    /// <returns>Serialized JSON String for series.json</returns>
    private string GetSeriesInfoJson()
    {
        SeriesInfo si = new (new Metadata(this.sortName, this.year.ToString() ?? string.Empty, this.status, this.description ?? ""));
        return System.Text.Json.JsonSerializer.Serialize(si);
    }

    //Only for series.json
    private struct SeriesInfo
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local we need it, trust
        [JsonRequired]public Metadata metadata { get; }
        public SeriesInfo(Metadata metadata) => this.metadata = metadata;
    }

    //Only for series.json what an abomination, why are all the fields not-null????
    private struct Metadata
    {
        // ReSharper disable UnusedAutoPropertyAccessor.Local we need them all, trust me
        [JsonRequired] public string type { get; }
        [JsonRequired] public string publisher { get; }
        // ReSharper disable twice IdentifierTypo
        [JsonRequired] public int comicid  { get; }
        [JsonRequired] public string booktype { get; }
        // ReSharper disable InconsistentNaming This one property is capitalized. Why?
        [JsonRequired] public string ComicImage { get; }
        [JsonRequired] public int total_issues { get; }
        [JsonRequired] public string publication_run { get; }
        [JsonRequired]public string name { get; }
        [JsonRequired]public string year { get; }
        [JsonRequired]public string status { get; }
        [JsonRequired]public string description_text { get; }
        [JsonIgnore] public static string[] continuing = new[]
        {
            "ongoing",
            "hiatus",
            "in corso",
            "in pausa"
        };
        [JsonIgnore] public static string[] ended = new[]
        {
            "completed",
            "cancelled",
            "discontinued",
            "finito",
            "cancellato",
            "droppato"
        };
        
        public Metadata(string name, string year, string status, string description_text)
        {
            this.name = name;
            this.year = year;
            if(continuing.Contains(status.ToLower()))
                this.status = "Continuing";
            else if(ended.Contains(status.ToLower()))
                this.status = "Ended";
            else
                this.status = status;
            this.description_text = description_text;
            
            //kill it with fire, but otherwise Komga will not parse
            type = "Manga";
            publisher = "";
            comicid = 0;
            booktype = "";
            ComicImage = "";
            total_issues = 0;
            publication_run = "";
        }
    }
}