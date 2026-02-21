package com.getsharex.xerahs.mobile.core.data

import android.database.sqlite.SQLiteDatabase
import com.getsharex.xerahs.mobile.core.common.Paths
import com.getsharex.xerahs.mobile.core.domain.HistoryEntry
import com.google.gson.Gson
import java.io.File

/**
 * SQLite history using same schema as C# HistoryManagerSQLite for DB compatibility.
 * Table: History (Id, FileName, FilePath, DateTime, Type, Host, URL, ThumbnailURL, DeletionURL, ShortenedURL, Tags).
 */
class HistoryRepository(
    private val gson: Gson = Gson()
) {
    private val dbPath: String?
        get() = Paths.historyFilePath

    private fun openDb(): SQLiteDatabase? {
        val path = dbPath ?: return null
        Paths.historyFolder?.mkdirs()
        return try {
            SQLiteDatabase.openOrCreateDatabase(path, null)
        } catch (e: Exception) {
            null
        }
    }

    init {
        openDb()?.use { db ->
            db.execSQL("""
                CREATE TABLE IF NOT EXISTS History (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FileName TEXT,
                    FilePath TEXT,
                    DateTime TEXT,
                    Type TEXT,
                    Host TEXT,
                    URL TEXT,
                    ThumbnailURL TEXT,
                    DeletionURL TEXT,
                    ShortenedURL TEXT,
                    Tags TEXT
                )
            """.trimIndent())
        }
    }

    fun getRecentEntries(limit: Int): List<HistoryEntry> {
        if (limit <= 0) return emptyList()
        val path = dbPath ?: return emptyList()
        if (!File(path).exists()) return emptyList()
        val db = openDb() ?: return emptyList()
        return try {
            db.rawQuery(
                "SELECT * FROM History ORDER BY DateTime DESC LIMIT ?",
                arrayOf(limit.toString())
            ).use { cursor ->
                val list = mutableListOf<HistoryEntry>()
                val idxId = cursor.getColumnIndex("Id")
                val idxFileName = cursor.getColumnIndex("FileName")
                val idxFilePath = cursor.getColumnIndex("FilePath")
                val idxDateTime = cursor.getColumnIndex("DateTime")
                val idxType = cursor.getColumnIndex("Type")
                val idxHost = cursor.getColumnIndex("Host")
                val idxURL = cursor.getColumnIndex("URL")
                val idxThumb = cursor.getColumnIndex("ThumbnailURL")
                val idxDel = cursor.getColumnIndex("DeletionURL")
                val idxShort = cursor.getColumnIndex("ShortenedURL")
                val idxTags = cursor.getColumnIndex("Tags")
                while (cursor.moveToNext()) {
                    list.add(
                        HistoryEntry(
                            id = if (idxId >= 0) cursor.getLong(idxId) else 0L,
                            fileName = if (idxFileName >= 0) cursor.getString(idxFileName) ?: "" else "",
                            filePath = if (idxFilePath >= 0) cursor.getString(idxFilePath) ?: "" else "",
                            dateTime = if (idxDateTime >= 0) cursor.getString(idxDateTime) ?: "" else "",
                            type = if (idxType >= 0) cursor.getString(idxType) ?: "" else "",
                            host = if (idxHost >= 0) cursor.getString(idxHost) ?: "" else "",
                            url = if (idxURL >= 0) cursor.getString(idxURL) ?: "" else "",
                            thumbnailUrl = if (idxThumb >= 0) cursor.getString(idxThumb) ?: "" else "",
                            deletionUrl = if (idxDel >= 0) cursor.getString(idxDel) ?: "" else "",
                            shortenedUrl = if (idxShort >= 0) cursor.getString(idxShort) ?: "" else "",
                            tags = parseTags(if (idxTags >= 0) cursor.getString(idxTags) else null)
                        )
                    )
                }
                list
            }
        } finally {
            db.close()
        }
    }

    fun deleteEntry(id: Long): Boolean {
        val db = openDb() ?: return false
        return try {
            db.delete("History", "Id = ?", arrayOf(id.toString())) > 0
        } finally {
            db.close()
        }
    }

    fun clearEntries(): Int {
        val db = openDb() ?: return 0
        return try {
            db.delete("History", null, null)
        } finally {
            db.close()
        }
    }

    fun insertEntry(
        fileName: String,
        filePath: String,
        type: String,
        host: String,
        url: String,
        thumbnailUrl: String = "",
        deletionUrl: String = "",
        shortenedUrl: String = "",
        tags: Map<String, String?> = emptyMap()
    ): Long {
        val db = openDb() ?: return -1L
        return try {
            val dateTime = java.text.SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", java.util.Locale.US).apply { timeZone = java.util.TimeZone.getTimeZone("UTC") }.format(java.util.Date())
            db.insert(
                "History",
                null,
                android.content.ContentValues().apply {
                    put("FileName", fileName)
                    put("FilePath", filePath)
                    put("DateTime", dateTime)
                    put("Type", type)
                    put("Host", host)
                    put("URL", url)
                    put("ThumbnailURL", thumbnailUrl)
                    put("DeletionURL", deletionUrl)
                    put("ShortenedURL", shortenedUrl)
                    put("Tags", gson.toJson(tags))
                }
            )
        } finally {
            db.close()
        }
    }

    private fun parseTags(tagsJson: String?): Map<String, String?> {
        if (tagsJson.isNullOrBlank()) return emptyMap()
        return try {
            val type = object : com.google.gson.reflect.TypeToken<Map<String, String?>>() {}
            gson.fromJson(tagsJson, type.type) ?: emptyMap()
        } catch (e: Exception) {
            emptyMap()
        }
    }
}
