﻿#region License

/**
 * Copyright (C) 2012 Jack Wakefield
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using Revise.Files.Attributes;
using Revise.Files.Exceptions;
using KeyNotFoundException = Revise.Files.Exceptions.KeyNotFoundException;

namespace Revise.Files {
    /// <summary>
    /// Provides the ability to create, open and save STL files.
    /// </summary>
    public class STL : FileLoader {
        #region Properties

        /// <summary>
        /// Gets or sets the table type.
        /// </summary>
        /// <value>
        /// The table type.
        /// </value>
        public TableType TableType {
            get;
            set;
        }

        /// <summary>
        /// Gets the number of languages.
        /// </summary>
        public int LanguageCount {
            get {
                return languageCount;
            }
        }

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int RowCount {
            get {
                return rows.Count;
            }
        }

        #endregion

        private static readonly int languageCount;

        private List<TableKey> keys;
        private List<TableRow> rows;

        /// <summary>
        /// Initializes the <see cref="Revise.Files.STL"/> class.
        /// </summary>
        static STL() {
            Array languages = Enum.GetValues(typeof(TableLanguage));
            languageCount = languages.Length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Revise.Files.STL"/> class.
        /// </summary>
        public STL() {
            keys = new List<TableKey>();
            rows = new List<TableRow>();

            Reset();
        }

        /// <summary>
        /// Gets the specified <see cref="Revise.Files.TableRow"/>.
        /// </summary>
        /// <exception cref="Revise.Exceptions.RowOutOfRangeException">Thrown when the specified row does not exist.</exception>
        public TableRow this[int row] {
            get {
                if (row < 0 || row > rows.Count - 1) {
                    throw new RowOutOfRangeException();
                }

                return rows[row];
            }
        }

        /// <summary>
        /// Gets the <see cref="Revise.Files.TableRow"/> matching the specified key.
        /// </summary>
        /// <exception cref="Revise.Exceptions.KeyNotFoundException">Thrown when the specified key does not exist.</exception>
        public TableRow this[string key] {
            get {
                for (int i = 0; i < keys.Count; i++) {
                    if (string.Compare(key, keys[i].Key, false) == 0) {
                        return rows[i];
                    }
                }

                throw new KeyNotFoundException(key);
            }
        }

        /// <summary>
        /// Loads the file from the specified stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        public override void Load(Stream stream) {
            BinaryReader reader = new BinaryReader(stream);

            string typeValue = reader.ReadString();
            TableType = GetTableType(typeValue);

            int rowCount = reader.ReadInt32();

            for (int i = 0; i < rowCount; i++) {
                TableKey key = new TableKey();
                key.Key = reader.ReadString();
                key.ID = reader.ReadInt32();

                keys.Add(key);
            }

            int languageCount = reader.ReadInt32();

            for (int j = 0; j < rowCount; j++) {
                TableRow row = new TableRow(languageCount);
                rows.Add(row);
            }

            for (int i = 0; i < languageCount; i++) {
                TableLanguage language = (TableLanguage)i;

                int languageOffset = reader.ReadInt32();
                long nextLanguageOffset = stream.Position;

                stream.Seek(languageOffset, SeekOrigin.Begin);

                for (int j = 0; j < rowCount; j++) {
                    int rowOffset = reader.ReadInt32();
                    long nextRowOffset = stream.Position;

                    stream.Seek(rowOffset, SeekOrigin.Begin);

                    TableRow row = rows[j];
                    row.SetText(reader.ReadString(), language);

                    if (TableType == TableType.Item || TableType == TableType.Quest) {
                        row.SetDescription(reader.ReadString(), language);

                        if (TableType == TableType.Quest) {
                            row.SetStartMessage(reader.ReadString(), language);
                            row.SetEndMessage(reader.ReadString(), language);
                        }
                    }

                    if (j < rowCount - 1) {
                        stream.Seek(nextRowOffset, SeekOrigin.Begin);
                    }
                }

                if (i < languageCount - 1) {
                    stream.Seek(nextLanguageOffset, SeekOrigin.Begin);
                }
            }
        }

        /// <summary>
        /// Saves the file to the specified stream.
        /// </summary>
        /// <param name="stream">The stream to save to.</param>
        public override void Save(Stream stream) {
            BinaryWriter writer = new BinaryWriter(stream);

            string tableType = TableType.GetAttributeValue<TableTypeIdentifierAttribute, string>(x => x.Value);
            writer.Write(tableType);

            writer.Write(rows.Count);

            keys.ForEach(key => {
                writer.Write(key.Key);
                writer.Write(key.ID);
            });

            writer.Write(LanguageCount);

            long languageOffsets = stream.Position;

            for (int i = 0; i < LanguageCount; i++) {
                writer.Write(0);
            }

            long[] rowOffsetValues = new long[rows.Count];

            for (int i = 0; i < LanguageCount; i++) {
                TableLanguage language = (TableLanguage)i;

                long rowOffsets = stream.Position;
                
                for (int j = 0; j < rows.Count; j++) {
                    writer.Write(0);
                }

                for (int j = 0; j < rows.Count; j++) {
                    TableRow row = rows[j];
                    rowOffsetValues[j] = stream.Position;

                    writer.Write(row.GetText(language));

                    if (TableType == TableType.Item || TableType == TableType.Quest) {
                        writer.Write(row.GetDescription(language));

                        if (TableType == TableType.Quest) {
                            writer.Write(row.GetStartMessage(language));
                            writer.Write(row.GetEndMessage(language));
                        }
                    }
                }

                long position = stream.Position;
                stream.Seek(rowOffsets, SeekOrigin.Begin);

                for (int j = 0; j < rowOffsetValues.Length; j++) {
                    writer.Write((int)rowOffsetValues[j]);
                }

                stream.Seek(languageOffsets + (i * sizeof(int)), SeekOrigin.Begin);
                writer.Write((int)rowOffsets);

                stream.Seek(position, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Adds a new row using the specified key and id.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="id">The id.</param>
        /// <returns>The row created.</returns>
        public TableRow AddRow(string key, int id) {
            TableKey tableKey = new TableKey();
            tableKey.Key = key;
            tableKey.ID = id;

            TableRow tableRow = new TableRow(LanguageCount);

            keys.Add(tableKey);
            rows.Add(tableRow);

            return tableRow;
        }

        /// <summary>
        /// Removes the row matching the specified key.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <exception cref="Revise.Exceptions.KeyNotFoundException">Thrown when the specified key does not exist.</exception>
        public void RemoveRow(string key) {
            for (int i = 0; i < keys.Count; i++) {
                if (string.Compare(key, keys[i].Key, false) == 0) {
                    RemoveRow(i);
                    return;
                }
            }

            throw new KeyNotFoundException(key);
        }

        /// <summary>
        /// Removes the row.
        /// </summary>
        /// <param name="row">The row to remove.</param>
        /// <exception cref="Revise.Exceptions.RowOutOfRangeException">Thrown when the specified row is out of range.</exception>
        public void RemoveRow(int row) {
            if (row < 0 || row > rows.Count - 1) {
                throw new RowOutOfRangeException();
            }

            keys.RemoveAt(row);
            rows.RemoveAt(row);
        }

        /// <summary>
        /// Removes all rows and keys.
        /// </summary>
        public void Clear() {
            keys.Clear();
            rows.Clear();
        }

        /// <summary>
        /// Resets properties to their default values.
        /// </summary>
        public override void Reset() {
            base.Reset();

            TableType = TableType.Normal;
            Clear();
        }

        /// <summary>
        /// Gets the table type from the specified string.
        /// </summary>
        /// <param name="value">The table type string.</param>
        /// <returns>The table type value.</returns>
        /// <exception cref="Revise.Exceptions.InvalidTableTypeException">Thrown when the specified string does not match a table type.</exception>
        public static TableType GetTableType(string value) {
            Array values = Enum.GetValues(typeof(TableType));

            for (int i = 0; i < values.Length; i++) {
                TableType type = (TableType)values.GetValue(i);
                string identifier = type.GetAttributeValue<TableTypeIdentifierAttribute, string>(x => x.Value);

                if (string.Compare(identifier, value, false) == 0) {
                    return type;
                }
            }

            throw new InvalidTableTypeException(value);
        }
    }
}