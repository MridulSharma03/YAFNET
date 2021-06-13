﻿//
// ServiceStack.OrmLite: Light-weight POCO ORM for .NET and Mono
//
// Authors:
//   Demis Bellot (demis.bellot@gmail.com)
//
// Copyright 2013 ServiceStack, Inc. All Rights Reserved.
//
// Licensed under the same terms of ServiceStack.
//

namespace ServiceStack.OrmLite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    using ServiceStack.DataAnnotations;
    using ServiceStack.Logging;
    using ServiceStack.OrmLite.Converters;
    using ServiceStack.Script;
    using ServiceStack.Text;

    public abstract class OrmLiteDialectProviderBase<TDialect>
        : IOrmLiteDialectProvider
        where TDialect : IOrmLiteDialectProvider
    {
        protected static readonly ILog Log = LogManager.GetLogger(typeof(IOrmLiteDialectProvider));

        protected OrmLiteDialectProviderBase()
        {
            this.Variables = new Dictionary<string, string>();
            this.StringSerializer = new JsvStringSerializer();
        }

        #region ADO.NET supported types

        /* ADO.NET UNDERSTOOD DATA TYPES:
                    COUNTER	DbType.Int64
                    AUTOINCREMENT	DbType.Int64
                    IDENTITY	DbType.Int64
                    LONG	DbType.Int64
                    TINYINT	DbType.Byte
                    INTEGER	DbType.Int64
                    INT	DbType.Int32
                    VARCHAR	DbType.String
                    NVARCHAR	DbType.String
                    CHAR	DbType.String
                    NCHAR	DbType.String
                    TEXT	DbType.String
                    NTEXT	DbType.String
                    STRING	DbType.String
                    DOUBLE	DbType.Double
                    FLOAT	DbType.Double
                    REAL	DbType.Single
                    BIT	DbType.Boolean
                    YESNO	DbType.Boolean
                    LOGICAL	DbType.Boolean
                    BOOL	DbType.Boolean
                    NUMERIC	DbType.Decimal
                    DECIMAL	DbType.Decimal
                    MONEY	DbType.Decimal
                    CURRENCY	DbType.Decimal
                    TIME	DbType.DateTime
                    DATE	DbType.DateTime
                    TIMESTAMP	DbType.DateTime
                    DATETIME	DbType.DateTime
                    BLOB	DbType.Binary
                    BINARY	DbType.Binary
                    VARBINARY	DbType.Binary
                    IMAGE	DbType.Binary
                    GENERAL	DbType.Binary
                    OLEOBJECT	DbType.Binary
                    GUID	DbType.Guid
                    UNIQUEIDENTIFIER	DbType.Guid
                    MEMO	DbType.String
                    NOTE	DbType.String
                    LONGTEXT	DbType.String
                    LONGCHAR	DbType.String
                    SMALLINT	DbType.Int16
                    BIGINT	DbType.Int64
                    LONGVARCHAR	DbType.String
                    SMALLDATE	DbType.DateTime
                    SMALLDATETIME	DbType.DateTime
                 */
        #endregion

        protected void InitColumnTypeMap()
        {
            this.EnumConverter = new EnumConverter();
            this.RowVersionConverter = new RowVersionConverter();
            this.ReferenceTypeConverter = new ReferenceTypeConverter();
            this.ValueTypeConverter = new ValueTypeConverter();

            this.RegisterConverter<string>(new StringConverter());
            this.RegisterConverter<char>(new CharConverter());
            this.RegisterConverter<char[]>(new CharArrayConverter());
            this.RegisterConverter<byte[]>(new ByteArrayConverter());

            this.RegisterConverter<byte>(new ByteConverter());
            this.RegisterConverter<sbyte>(new SByteConverter());
            this.RegisterConverter<short>(new Int16Converter());
            this.RegisterConverter<ushort>(new UInt16Converter());
            this.RegisterConverter<int>(new Int32Converter());
            this.RegisterConverter<uint>(new UInt32Converter());
            this.RegisterConverter<long>(new Int64Converter());
            this.RegisterConverter<ulong>(new UInt64Converter());

            this.RegisterConverter<ulong>(new UInt64Converter());

            this.RegisterConverter<float>(new FloatConverter());
            this.RegisterConverter<double>(new DoubleConverter());
            this.RegisterConverter<decimal>(new DecimalConverter());

            this.RegisterConverter<Guid>(new GuidConverter());
            this.RegisterConverter<TimeSpan>(new TimeSpanAsIntConverter());
            this.RegisterConverter<DateTime>(new DateTimeConverter());
            this.RegisterConverter<DateTimeOffset>(new DateTimeOffsetConverter());
        }

        public string GetColumnTypeDefinition(Type columnType, int? fieldLength, int? scale)
        {
            var converter = this.GetConverter(columnType);
            if (converter != null)
            {
                if (converter is IHasColumnDefinitionPrecision customPrecisionConverter)
                    return customPrecisionConverter.GetColumnDefinition(fieldLength, scale);

                if (converter is IHasColumnDefinitionLength customLengthConverter)
                    return customLengthConverter.GetColumnDefinition(fieldLength);

                if (string.IsNullOrEmpty(converter.ColumnDefinition))
                    throw new ArgumentException($"{converter.GetType().Name} requires a ColumnDefinition");

                return converter.ColumnDefinition;
            }

            var stringConverter = columnType.IsRefType() ? this.ReferenceTypeConverter :
                columnType.IsEnum ? this.EnumConverter : (IHasColumnDefinitionLength)this.ValueTypeConverter;

            return stringConverter.GetColumnDefinition(fieldLength);
        }

        public virtual void InitDbParam(IDbDataParameter dbParam, Type columnType)
        {
            var converter = this.GetConverterBestMatch(columnType);
            converter.InitDbParam(dbParam, columnType);
        }

        public abstract IDbDataParameter CreateParam();

        public Dictionary<string, string> Variables { get; set; }

        public IOrmLiteExecFilter ExecFilter { get; set; }

        public Dictionary<Type, IOrmLiteConverter> Converters = new Dictionary<Type, IOrmLiteConverter>();

        public string AutoIncrementDefinition = "AUTOINCREMENT"; // SqlServer express limit

        public DecimalConverter DecimalConverter => (DecimalConverter)this.Converters[typeof(decimal)];

        public StringConverter StringConverter => (StringConverter)this.Converters[typeof(string)];

        public Action<IDbConnection> OnOpenConnection { get; set; }

        public string ParamString { get; set; } = "@";

        public INamingStrategy NamingStrategy { get; set; } = new OrmLiteNamingStrategyBase();

        public IStringSerializer StringSerializer { get; set; }

        private Func<string, string> paramNameFilter;

        public Func<string, string> ParamNameFilter
        {
            get => this.paramNameFilter ?? OrmLiteConfig.ParamNameFilter;
            set => this.paramNameFilter = value;
        }

        public string DefaultValueFormat = " DEFAULT ({0})";

        private EnumConverter enumConverter;

        public EnumConverter EnumConverter
        {
            get => this.enumConverter;
            set
            {
                value.DialectProvider = this;
                this.enumConverter = value;
            }
        }

        private RowVersionConverter rowVersionConverter;

        public RowVersionConverter RowVersionConverter
        {
            get => this.rowVersionConverter;
            set
            {
                value.DialectProvider = this;
                this.rowVersionConverter = value;
            }
        }

        private ReferenceTypeConverter referenceTypeConverter;

        public ReferenceTypeConverter ReferenceTypeConverter
        {
            get => this.referenceTypeConverter;
            set
            {
                value.DialectProvider = this;
                this.referenceTypeConverter = value;
            }
        }

        private ValueTypeConverter valueTypeConverter;

        public ValueTypeConverter ValueTypeConverter
        {
            get => this.valueTypeConverter;
            set
            {
                value.DialectProvider = this;
                this.valueTypeConverter = value;
            }
        }

        public void RemoveConverter<T>()
        {
            if (this.Converters.TryRemove(typeof(T), out var converter))
                converter.DialectProvider = null;
        }

        public void RegisterConverter<T>(IOrmLiteConverter converter)
        {
            if (converter == null)
                throw new ArgumentNullException(nameof(converter));

            converter.DialectProvider = this;
            this.Converters[typeof(T)] = converter;
        }

        public IOrmLiteConverter GetConverter(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return this.Converters.TryGetValue(type, out IOrmLiteConverter converter) ? converter : null;
        }

        public virtual bool ShouldQuoteValue(Type fieldType)
        {
            var converter = this.GetConverter(fieldType);
            return converter == null || converter is NativeValueOrmLiteConverter;
        }

        public virtual object FromDbRowVersion(Type fieldType, object value)
        {
            return this.RowVersionConverter.FromDbValue(fieldType, value);
        }

        public IOrmLiteConverter GetConverterBestMatch(Type type)
        {
            if (type == typeof(RowVersionConverter))
                return this.RowVersionConverter;

            var converter = this.GetConverter(type);
            if (converter != null)
                return converter;

            if (type.IsEnum)
                return this.EnumConverter;

            return type.IsRefType() ? (IOrmLiteConverter)this.ReferenceTypeConverter : this.ValueTypeConverter;
        }

        public virtual IOrmLiteConverter GetConverterBestMatch(FieldDefinition fieldDef)
        {
            var fieldType = Nullable.GetUnderlyingType(fieldDef.FieldType) ?? fieldDef.FieldType;

            if (fieldDef.IsRowVersion)
                return this.RowVersionConverter;

            if (this.Converters.TryGetValue(fieldType, out var converter))
                return converter;

            if (fieldType.IsEnum)
                return this.EnumConverter;

            return fieldType.IsRefType() ? (IOrmLiteConverter)this.ReferenceTypeConverter : this.ValueTypeConverter;
        }

        public virtual object ToDbValue(object value, Type type)
        {
            if (value == null || value is DBNull)
                return null;

            var converter = this.GetConverterBestMatch(type);
            try
            {
                return converter.ToDbValue(type, value);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {converter.GetType().Name}.ToDbValue() value '{value.GetType().Name}' and Type '{type.Name}'",
                    ex);
                throw;
            }
        }

        public virtual object FromDbValue(object value, Type type)
        {
            if (value == null || value is DBNull)
                return null;

            var converter = this.GetConverterBestMatch(type);
            try
            {
                return converter.FromDbValue(type, value);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {converter.GetType().Name}.FromDbValue() value '{value.GetType().Name}' and Type '{type.Name}'",
                    ex);
                throw;
            }
        }

        public object GetValue(IDataReader reader, int columnIndex, Type type)
        {
            if (this.Converters.TryGetValue(type, out var converter))
                return converter.GetValue(reader, columnIndex, null);

            return reader.GetValue(columnIndex);
        }

        public virtual int GetValues(IDataReader reader, object[] values)
        {
            return reader.GetValues(values);
        }

        public abstract IDbConnection CreateConnection(string filePath, Dictionary<string, string> options);

        public virtual string GetQuotedValue(string paramValue)
        {
            return "'" + paramValue.Replace("'", "''") + "'";
        }

        public virtual string GetSchemaName(string schema)
        {
            return this.NamingStrategy.GetSchemaName(schema);
        }

        public virtual string GetTableName(ModelDefinition modelDef) =>
            this.GetTableName(modelDef.ModelName, modelDef.Schema, useStrategy: true);

        public virtual string GetTableName(ModelDefinition modelDef, bool useStrategy) =>
            this.GetTableName(modelDef.ModelName, modelDef.Schema, useStrategy);

        public virtual string GetTableName(string table, string schema = null) =>
            this.GetTableName(table, schema, useStrategy: true);

        public virtual string GetTableName(string table, string schema, bool useStrategy)
        {
            if (useStrategy)
            {
                return schema != null
                    ? $"{this.QuoteIfRequired(this.NamingStrategy.GetSchemaName(schema))}.{this.QuoteIfRequired(this.NamingStrategy.GetTableName(table))}"
                    : this.QuoteIfRequired(this.NamingStrategy.GetTableName(table));
            }

            return schema != null
                ? $"{this.QuoteIfRequired(schema)}.{this.QuoteIfRequired(table)}"
                : this.QuoteIfRequired(table);
        }

        public virtual string GetTableNameWithBrackets<T>()
        {
            var modelDef = typeof(T).GetModelDefinition();
            return this.GetTableNameWithBrackets(modelDef.ModelName, modelDef.Schema);
        }

        public virtual string GetTableNameWithBrackets(ModelDefinition modelDef)
        {
            return this.GetTableNameWithBrackets(modelDef.ModelName, modelDef.Schema);
        }

        public virtual string GetTableNameWithBrackets(string tableName, string schema = null)
        {
            return $"[{this.NamingStrategy.GetSchemaName(schema)}].[{this.NamingStrategy.GetTableName(tableName)}]";
        }

        public virtual string GetQuotedTableName(ModelDefinition modelDef)
        {
            return this.GetQuotedTableName(modelDef.ModelName, modelDef.Schema);
        }

        public virtual string GetQuotedTableName(string tableName, string schema = null)
        {
            /*if (schema == null)
                return GetQuotedName(NamingStrategy.GetTableName(tableName));*/
            var escapedSchema = this.NamingStrategy.GetSchemaName(schema).Replace(".", "\".\"");

            return
                $"{this.GetQuotedName(escapedSchema)}.{this.GetQuotedName(this.NamingStrategy.GetTableName(tableName))}";
        }

        public virtual string GetQuotedTableName(string tableName, string schema, bool useStrategy) =>
            this.GetQuotedName(this.GetTableName(tableName, schema, useStrategy));

        public virtual string GetQuotedColumnName(string columnName)
        {
            return this.GetQuotedName(this.NamingStrategy.GetColumnName(columnName));
        }

        public virtual bool ShouldQuote(string name) =>
            !string.IsNullOrEmpty(name) && (name.IndexOf(' ') >= 0 || name.IndexOf('.') >= 0);

        public virtual string QuoteIfRequired(string name)
        {
            return this.ShouldQuote(name) ? this.GetQuotedName(name) : name;
        }

        public virtual string GetQuotedName(string name) =>
            name == null ? null : name.FirstCharEquals('"') ? name : '"' + name + '"';

        public virtual string GetQuotedName(string name, string schema)
        {
            return schema != null
                ? $"{this.GetQuotedName(schema)}.{this.GetQuotedName(name)}"
                : this.GetQuotedName(name);
        }

        public virtual string SanitizeFieldNameForParamName(string fieldName)
        {
            return OrmLiteConfig.SanitizeFieldNameForParamNameFn(fieldName);
        }

        public virtual string GetColumnDefinition(FieldDefinition fieldDef)
        {
            var fieldDefinition = ResolveFragment(fieldDef.CustomFieldDefinition) ??
                                  GetColumnTypeDefinition(fieldDef.ColumnType, fieldDef.FieldLength, fieldDef.Scale);

            var sql = StringBuilderCache.Allocate();
            sql.Append($"{GetQuotedColumnName(fieldDef.FieldName)} {fieldDefinition}");

            if (fieldDef.IsPrimaryKey)
            {
                sql.Append(" PRIMARY KEY");
                if (fieldDef.AutoIncrement)
                {
                    sql.Append(" ").Append(AutoIncrementDefinition);
                }
            }
            else
            {
                sql.Append(fieldDef.IsNullable ? " NULL" : " NOT NULL");
            }

            if (fieldDef.IsUniqueConstraint)
            {
                sql.Append(" UNIQUE");
            }

            var defaultValue = this.GetDefaultValue(fieldDef);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                sql.AppendFormat(this.DefaultValueFormat, defaultValue);
            }

            return StringBuilderCache.ReturnAndFree(sql);
        }

        public virtual string GetColumnDefinition(FieldDefinition fieldDef, ModelDefinition modelDef)
        {
            var fieldDefinition = ResolveFragment(fieldDef.CustomFieldDefinition) ??
                                  GetColumnTypeDefinition(fieldDef.ColumnType, fieldDef.FieldLength, fieldDef.Scale);

            var sql = StringBuilderCache.Allocate();
            sql.Append($"{GetQuotedColumnName(fieldDef.FieldName)} {fieldDefinition}");

            // Check for Composite PrimaryKey First
            if (modelDef.CompositePrimaryKeys.Any())
            {
                sql.Append(fieldDef.IsNullable ? " NULL" : " NOT NULL");
            }
            else
            {
                if (fieldDef.IsPrimaryKey)
                {
                    sql.Append(" PRIMARY KEY");
                    if (fieldDef.AutoIncrement)
                    {
                        sql.Append(" ").Append(AutoIncrementDefinition);
                    }
                }
                else
                {
                    sql.Append(fieldDef.IsNullable ? " NULL" : " NOT NULL");
                }
            }

            if (fieldDef.IsUniqueConstraint)
            {
                sql.Append(" UNIQUE");
            }

            var defaultValue = this.GetDefaultValue(fieldDef);
            if (!string.IsNullOrEmpty(defaultValue))
            {
                sql.AppendFormat(this.DefaultValueFormat, defaultValue);
            }

            return StringBuilderCache.ReturnAndFree(sql);
        }

        public virtual string SelectIdentitySql { get; set; }

        public virtual long GetLastInsertId(IDbCommand dbCmd)
        {
            if (this.SelectIdentitySql == null)
                throw new NotImplementedException(
                    "Returning last inserted identity is not implemented on this DB Provider.");

            dbCmd.CommandText = this.SelectIdentitySql;
            return dbCmd.ExecLongScalar();
        }

        public virtual string GetLastInsertIdSqlSuffix<T>()
        {
            if (this.SelectIdentitySql == null)
                throw new NotImplementedException(
                    "Returning last inserted identity is not implemented on this DB Provider.");

            return "; " + this.SelectIdentitySql;
        }

        public virtual bool IsFullSelectStatement(string sql) =>
            !string.IsNullOrEmpty(sql) && sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase);

        // Fmt
        public virtual string ToSelectStatement(Type tableType, string sqlFilter, params object[] filterParams)
        {
            if (this.IsFullSelectStatement(sqlFilter))
                return sqlFilter.SqlFmt(this, filterParams);

            var modelDef = tableType.GetModelDefinition();
            var sql = StringBuilderCache.Allocate();
            sql.Append($"SELECT {this.GetColumnNames(modelDef)} FROM {this.GetQuotedTableName(modelDef)}");

            if (string.IsNullOrEmpty(sqlFilter))
                return StringBuilderCache.ReturnAndFree(sql);

            sqlFilter = sqlFilter.SqlFmt(this, filterParams);
            if (!sqlFilter.StartsWith("ORDER ", StringComparison.OrdinalIgnoreCase) &&
                !sqlFilter.StartsWith("LIMIT ", StringComparison.OrdinalIgnoreCase))
            {
                sql.Append(" WHERE ");
            }

            sql.Append(sqlFilter);

            return StringBuilderCache.ReturnAndFree(sql);
        }

        public virtual string ToSelectStatement(
            ModelDefinition modelDef,
            string selectExpression,
            string bodyExpression,
            string orderByExpression = null,
            int? offset = null,
            int? rows = null)
        {
            var sb = StringBuilderCache.Allocate();
            sb.Append(selectExpression);
            sb.Append(bodyExpression);
            if (orderByExpression != null)
            {
                sb.Append(orderByExpression);
            }

            if (offset != null || rows != null)
            {
                sb.Append("\n");
                sb.Append(this.SqlLimit(offset, rows));
            }

            return StringBuilderCache.ReturnAndFree(sb);
        }

        public virtual SelectItem GetRowVersionSelectColumn(FieldDefinition field, string tablePrefix = null)
        {
            return new SelectItemColumn(this, field.FieldName, null, tablePrefix);
        }

        public virtual string GetRowVersionColumn(FieldDefinition field, string tablePrefix = null)
        {
            return this.GetRowVersionSelectColumn(field, tablePrefix).ToString();
        }

        public virtual string GetColumnNames(ModelDefinition modelDef)
        {
            return this.GetColumnNames(modelDef, null).ToSelectString();
        }

        public virtual SelectItem[] GetColumnNames(ModelDefinition modelDef, string tablePrefix)
        {
            var quotedPrefix = tablePrefix != null
                ? this.GetQuotedTableName(tablePrefix, modelDef.Schema)
                : string.Empty;

            var sqlColumns = new SelectItem[modelDef.FieldDefinitions.Count];
            for (var i = 0; i < sqlColumns.Length; ++i)
            {
                var field = modelDef.FieldDefinitions[i];

                if (field.CustomSelect != null)
                {
                    sqlColumns[i] = new SelectItemExpression(this, field.CustomSelect, field.FieldName);
                }
                else if (field.IsRowVersion)
                {
                    sqlColumns[i] = this.GetRowVersionSelectColumn(field, quotedPrefix);
                }
                else
                {
                    sqlColumns[i] = new SelectItemColumn(this, field.FieldName, null, quotedPrefix);
                }
            }

            return sqlColumns;
        }

        protected virtual bool ShouldSkipInsert(FieldDefinition fieldDef) => fieldDef.ShouldSkipInsert();

        public virtual string ColumnNameOnly(string columnExpr)
        {
            var nameOnly = columnExpr.LastRightPart('.');
            var ret = nameOnly.StripDbQuotes();
            return ret;
        }

        public virtual FieldDefinition[] GetInsertFieldDefinitions(
            ModelDefinition modelDef,
            ICollection<string> insertFields)
        {
            var insertColumns = insertFields?.Map(this.ColumnNameOnly);
            return insertColumns != null
                ? this.NamingStrategy.GetType() == typeof(OrmLiteNamingStrategyBase)
                    ?
                    modelDef.GetOrderedFieldDefinitions(insertColumns)
                    : modelDef.GetOrderedFieldDefinitions(
                        insertColumns,
                        name => this.NamingStrategy.GetColumnName(name))
                : modelDef.FieldDefinitionsArray;
        }

        public virtual string ToInsertRowStatement(
            IDbCommand cmd,
            object objWithProperties,
            ICollection<string> insertFields = null)
        {
            var sbColumnNames = StringBuilderCache.Allocate();
            var sbColumnValues = StringBuilderCacheAlt.Allocate();
            var modelDef = objWithProperties.GetType().GetModelDefinition();

            var fieldDefs = this.GetInsertFieldDefinitions(modelDef, insertFields);
            foreach (var fieldDef in fieldDefs)
            {
                if (this.ShouldSkipInsert(fieldDef) && !fieldDef.AutoId)
                    continue;

                if (sbColumnNames.Length > 0)
                    sbColumnNames.Append(",");
                if (sbColumnValues.Length > 0)
                    sbColumnValues.Append(",");

                try
                {
                    sbColumnNames.Append(this.GetQuotedColumnName(fieldDef.FieldName));
                    sbColumnValues.Append(this.GetParam(this.SanitizeFieldNameForParamName(fieldDef.FieldName)));

                    var p = this.AddParameter(cmd, fieldDef);
                    p.Value = this.GetFieldValue(fieldDef, fieldDef.GetValue(objWithProperties)) ?? DBNull.Value;
                }
                catch (Exception ex)
                {
                    Log.Error("ERROR in ToInsertRowStatement(): " + ex.Message, ex);
                    throw;
                }
            }

            var sql =
                $"INSERT INTO {this.GetQuotedTableName(modelDef)} ({StringBuilderCache.ReturnAndFree(sbColumnNames)}) " +
                $"VALUES ({StringBuilderCacheAlt.ReturnAndFree(sbColumnValues)})";

            return sql;
        }

        public virtual string ToInsertStatement<T>(IDbCommand dbCmd, T item, ICollection<string> insertFields = null)
        {
            dbCmd.Parameters.Clear();
            var dialectProvider = dbCmd.GetDialectProvider();
            dialectProvider.PrepareParameterizedInsertStatement<T>(dbCmd, insertFields);

            if (string.IsNullOrEmpty(dbCmd.CommandText))
                return null;

            dialectProvider.SetParameterValues<T>(dbCmd, item);

            return this.MergeParamsIntoSql(dbCmd.CommandText, this.ToArray(dbCmd.Parameters));
        }

        protected virtual object GetInsertDefaultValue(FieldDefinition fieldDef)
        {
            if (!fieldDef.AutoId)
                return null;
            if (fieldDef.FieldType == typeof(Guid))
                return Guid.NewGuid();
            return null;
        }

        public virtual void PrepareParameterizedInsertStatement<T>(
            IDbCommand cmd,
            ICollection<string> insertFields = null,
            Func<FieldDefinition, bool> shouldInclude = null)
        {
            var sbColumnNames = StringBuilderCache.Allocate();
            var sbColumnValues = StringBuilderCacheAlt.Allocate();
            var modelDef = typeof(T).GetModelDefinition();

            cmd.Parameters.Clear();

            var fieldDefs = this.GetInsertFieldDefinitions(modelDef, insertFields);
            foreach (var fieldDef in fieldDefs)
            {
                if (fieldDef.ShouldSkipInsert() && shouldInclude?.Invoke(fieldDef) != true)
                    continue;

                if (sbColumnNames.Length > 0)
                    sbColumnNames.Append(",");
                if (sbColumnValues.Length > 0)
                    sbColumnValues.Append(",");

                try
                {
                    sbColumnNames.Append(this.GetQuotedColumnName(fieldDef.FieldName));
                    sbColumnValues.Append(
                        this.GetParam(this.SanitizeFieldNameForParamName(fieldDef.FieldName), fieldDef.CustomInsert));

                    var p = this.AddParameter(cmd, fieldDef);

                    if (fieldDef.AutoId)
                    {
                        p.Value = this.GetInsertDefaultValue(fieldDef);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("ERROR in PrepareParameterizedInsertStatement(): " + ex.Message, ex);
                    throw;
                }
            }

            cmd.CommandText =
                $"INSERT INTO {this.GetQuotedTableName(modelDef)} ({StringBuilderCache.ReturnAndFree(sbColumnNames)}) " +
                $"VALUES ({StringBuilderCacheAlt.ReturnAndFree(sbColumnValues)})";
        }

        public virtual void PrepareInsertRowStatement<T>(IDbCommand dbCmd, Dictionary<string, object> args)
        {
            var sbColumnNames = StringBuilderCache.Allocate();
            var sbColumnValues = StringBuilderCacheAlt.Allocate();
            var modelDef = typeof(T).GetModelDefinition();

            dbCmd.Parameters.Clear();

            foreach (var entry in args)
            {
                var fieldDef = modelDef.AssertFieldDefinition(entry.Key);
                if (fieldDef.ShouldSkipInsert())
                    continue;

                var value = entry.Value;

                if (sbColumnNames.Length > 0)
                    sbColumnNames.Append(",");
                if (sbColumnValues.Length > 0)
                    sbColumnValues.Append(",");

                try
                {
                    sbColumnNames.Append(this.GetQuotedColumnName(fieldDef.FieldName));
                    sbColumnValues.Append(this.GetInsertParam(dbCmd, value, fieldDef));
                }
                catch (Exception ex)
                {
                    Log.Error("ERROR in PrepareInsertRowStatement(): " + ex.Message, ex);
                    throw;
                }
            }

            dbCmd.CommandText =
                $"INSERT INTO {this.GetQuotedTableName(modelDef)} ({StringBuilderCache.ReturnAndFree(sbColumnNames)}) " +
                $"VALUES ({StringBuilderCacheAlt.ReturnAndFree(sbColumnValues)})";
        }

        public virtual string ToUpdateStatement<T>(IDbCommand dbCmd, T item, ICollection<string> updateFields = null)
        {
            dbCmd.Parameters.Clear();
            var dialectProvider = dbCmd.GetDialectProvider();
            dialectProvider.PrepareParameterizedUpdateStatement<T>(dbCmd);

            if (string.IsNullOrEmpty(dbCmd.CommandText))
                return null;

            dialectProvider.SetParameterValues<T>(dbCmd, item);

            return this.MergeParamsIntoSql(dbCmd.CommandText, this.ToArray(dbCmd.Parameters));
        }

        IDbDataParameter[] ToArray(IDataParameterCollection dbParams)
        {
            var to = new IDbDataParameter[dbParams.Count];
            for (int i = 0; i < dbParams.Count; i++)
            {
                to[i] = (IDbDataParameter)dbParams[i];
            }

            return to;
        }

        public virtual string MergeParamsIntoSql(string sql, IEnumerable<IDbDataParameter> dbParams)
        {
            foreach (var dbParam in dbParams)
            {
                var quotedValue = dbParam.Value != null
                    ? this.GetQuotedValue(dbParam.Value, dbParam.Value.GetType())
                    : "null";

                var pattern = dbParam.ParameterName + @"(,|\s|\)|$)";
                var replacement = quotedValue.Replace("$", "$$") + "$1";
                sql = Regex.Replace(sql, pattern, replacement);
            }

            return sql;
        }

        public virtual bool PrepareParameterizedUpdateStatement<T>(
            IDbCommand cmd,
            ICollection<string> updateFields = null)
        {
            var sql = StringBuilderCache.Allocate();
            var sqlFilter = StringBuilderCacheAlt.Allocate();
            var modelDef = typeof(T).GetModelDefinition();
            var hadRowVersion = false;
            var updateAllFields = updateFields == null || updateFields.Count == 0;

            cmd.Parameters.Clear();

            foreach (var fieldDef in modelDef.FieldDefinitions)
            {
                if (fieldDef.ShouldSkipUpdate())
                    continue;

                try
                {
                    if ((fieldDef.IsPrimaryKey || fieldDef.IsRowVersion) && updateAllFields)
                    {
                        if (sqlFilter.Length > 0)
                            sqlFilter.Append(" AND ");

                        this.AppendFieldCondition(sqlFilter, fieldDef, cmd);

                        if (fieldDef.IsRowVersion)
                            hadRowVersion = true;

                        continue;
                    }

                    if (!updateAllFields && !updateFields.Contains(fieldDef.Name, StringComparer.OrdinalIgnoreCase))
                        continue;

                    if (sql.Length > 0)
                        sql.Append(", ");

                    sql.Append(this.GetQuotedColumnName(fieldDef.FieldName)).Append("=").Append(
                        this.GetParam(this.SanitizeFieldNameForParamName(fieldDef.FieldName), fieldDef.CustomUpdate));

                    this.AddParameter(cmd, fieldDef);
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex, "ERROR in PrepareParameterizedUpdateStatement(): " + ex.Message);
                }
            }

            if (sql.Length > 0)
            {
                var strFilter = StringBuilderCacheAlt.ReturnAndFree(sqlFilter);
                cmd.CommandText = $"UPDATE {this.GetQuotedTableName(modelDef)} " +
                                  $"SET {StringBuilderCache.ReturnAndFree(sql)} {(strFilter.Length > 0 ? "WHERE " + strFilter : string.Empty)}";
            }
            else
            {
                cmd.CommandText = string.Empty;
            }

            return hadRowVersion;
        }

        public virtual void AppendNullFieldCondition(StringBuilder sqlFilter, FieldDefinition fieldDef)
        {
            sqlFilter.Append(this.GetQuotedColumnName(fieldDef.FieldName)).Append(" IS NULL");
        }

        public virtual void AppendFieldCondition(StringBuilder sqlFilter, FieldDefinition fieldDef, IDbCommand cmd)
        {
            sqlFilter.Append(this.GetQuotedColumnName(fieldDef.FieldName)).Append("=").Append(
                this.GetParam(this.SanitizeFieldNameForParamName(fieldDef.FieldName)));

            this.AddParameter(cmd, fieldDef);
        }

        public virtual bool PrepareParameterizedDeleteStatement<T>(
            IDbCommand cmd,
            IDictionary<string, object> deleteFieldValues)
        {
            if (deleteFieldValues == null || deleteFieldValues.Count == 0)
                throw new ArgumentException("DELETE's must have at least 1 criteria");

            var sqlFilter = StringBuilderCache.Allocate();
            var modelDef = typeof(T).GetModelDefinition();
            var hadRowVersion = false;

            cmd.Parameters.Clear();

            foreach (var fieldDef in modelDef.FieldDefinitions)
            {
                if (fieldDef.ShouldSkipDelete())
                    continue;

                if (!deleteFieldValues.TryGetValue(fieldDef.Name, out var fieldValue))
                    continue;

                if (fieldDef.IsRowVersion)
                    hadRowVersion = true;

                try
                {
                    if (sqlFilter.Length > 0)
                        sqlFilter.Append(" AND ");

                    if (fieldValue != null)
                    {
                        this.AppendFieldCondition(sqlFilter, fieldDef, cmd);
                    }
                    else
                    {
                        this.AppendNullFieldCondition(sqlFilter, fieldDef);
                    }
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex, "ERROR in PrepareParameterizedDeleteStatement(): " + ex.Message);
                }
            }

            cmd.CommandText =
                $"DELETE FROM {this.GetQuotedTableName(modelDef)} WHERE {StringBuilderCache.ReturnAndFree(sqlFilter)}";

            return hadRowVersion;
        }

        public virtual void PrepareStoredProcedureStatement<T>(IDbCommand cmd, T obj)
        {
            cmd.CommandText = this.ToExecuteProcedureStatement(obj);
            cmd.CommandType = CommandType.StoredProcedure;
        }

        /// <summary>
        /// Used for adding updated DB params in INSERT and UPDATE statements
        /// </summary>
        protected IDbDataParameter AddParameter(IDbCommand cmd, FieldDefinition fieldDef)
        {
            var p = cmd.CreateParameter();
            this.SetParameter(fieldDef, p);
            this.InitUpdateParam(p);
            cmd.Parameters.Add(p);
            return p;
        }

        public virtual void SetParameter(FieldDefinition fieldDef, IDbDataParameter p)
        {
            p.ParameterName = this.GetParam(this.SanitizeFieldNameForParamName(fieldDef.FieldName));
            this.InitDbParam(p, fieldDef.ColumnType);
        }

        public virtual void EnableIdentityInsert<T>(IDbCommand cmd)
        {
        }

        public virtual Task EnableIdentityInsertAsync<T>(IDbCommand cmd, CancellationToken token = default) =>
            TypeConstants.EmptyTask;

        public virtual void DisableIdentityInsert<T>(IDbCommand cmd)
        {
        }

        public virtual Task DisableIdentityInsertAsync<T>(IDbCommand cmd, CancellationToken token = default) =>
            TypeConstants.EmptyTask;

        public virtual void EnableForeignKeysCheck(IDbCommand cmd)
        {
        }

        public virtual Task EnableForeignKeysCheckAsync(IDbCommand cmd, CancellationToken token = default) =>
            TypeConstants.EmptyTask;

        public virtual void DisableForeignKeysCheck(IDbCommand cmd)
        {
        }

        public virtual Task DisableForeignKeysCheckAsync(IDbCommand cmd, CancellationToken token = default) =>
            TypeConstants.EmptyTask;

        public virtual void SetParameterValues<T>(IDbCommand dbCmd, object obj)
        {
            var modelDef = GetModel(typeof(T));
            var fieldMap = this.GetFieldDefinitionMap(modelDef);

            foreach (IDataParameter p in dbCmd.Parameters)
            {
                var fieldName = this.ToFieldName(p.ParameterName);
                fieldMap.TryGetValue(fieldName, out var fieldDef);

                if (fieldDef == null)
                {
                    if (this.ParamNameFilter != null)
                    {
                        fieldDef = modelDef.GetFieldDefinition(
                            name => string.Equals(
                                this.ParamNameFilter(name),
                                fieldName,
                                StringComparison.OrdinalIgnoreCase));
                    }

                    if (fieldDef == null)
                        throw new ArgumentException($"Field Definition '{fieldName}' was not found");
                }

                if (fieldDef.AutoId && p.Value != null)
                {
                    var existingId = fieldDef.GetValue(obj);
                    if (existingId is Guid existingGuid && existingGuid != default(Guid))
                    {
                        p.Value = existingGuid; // Use existing value if not default
                    }

                    fieldDef.SetValue(obj, p.Value); // Auto populate default values
                    continue;
                }

                this.SetParameterValue<T>(fieldDef, p, obj);
            }
        }

        public Dictionary<string, FieldDefinition> GetFieldDefinitionMap(ModelDefinition modelDef)
        {
            return modelDef.GetFieldDefinitionMap(this.SanitizeFieldNameForParamName);
        }

        public virtual void SetParameterValue<T>(FieldDefinition fieldDef, IDataParameter p, object obj)
        {
            var value = this.GetValueOrDbNull<T>(fieldDef, obj);
            p.Value = value;

            if (p.Value is string s && p is IDbDataParameter dataParam && dataParam.Size > 0 &&
                s.Length > dataParam.Size)
            {
                // db param Size set in StringConverter
                dataParam.Size = s.Length;
            }
        }

        protected virtual object GetValue<T>(FieldDefinition fieldDef, object obj)
        {
            return this.GetFieldValue(fieldDef, fieldDef.GetValue(obj));
        }

        public object GetFieldValue(FieldDefinition fieldDef, object value)
        {
            if (value == null)
                return null;

            var converter = this.GetConverterBestMatch(fieldDef);
            try
            {
                return converter.ToDbValue(fieldDef.FieldType, value);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {converter.GetType().Name}.ToDbValue() for field '{fieldDef.Name}' of Type '{fieldDef.FieldType}' with value '{value.GetType().Name}'",
                    ex);
                throw;
            }
        }

        public object GetFieldValue(Type fieldType, object value)
        {
            if (value == null)
                return null;

            var converter = this.GetConverterBestMatch(fieldType);
            try
            {
                return converter.ToDbValue(fieldType, value);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {converter.GetType().Name}.ToDbValue() for field of Type '{fieldType}' with value '{value.GetType().Name}'",
                    ex);
                throw;
            }
        }

        protected virtual object GetValueOrDbNull<T>(FieldDefinition fieldDef, object obj)
        {
            var value = this.GetValue<T>(fieldDef, obj);
            if (value == null)
                return DBNull.Value;

            return value;
        }

        protected virtual object GetQuotedValueOrDbNull<T>(FieldDefinition fieldDef, object obj)
        {
            var value = fieldDef.GetValue(obj);

            if (value == null)
                return DBNull.Value;

            var unquotedVal = this.GetQuotedValue(value, fieldDef.FieldType).TrimStart('\'').TrimEnd('\'');

            if (string.IsNullOrEmpty(unquotedVal))
                return DBNull.Value;

            return unquotedVal;
        }

        public virtual void PrepareUpdateRowStatement(
            IDbCommand dbCmd,
            object objWithProperties,
            ICollection<string> updateFields = null)
        {
            var sql = StringBuilderCache.Allocate();
            var sqlFilter = StringBuilderCacheAlt.Allocate();
            var modelDef = objWithProperties.GetType().GetModelDefinition();
            var updateAllFields = updateFields == null || updateFields.Count == 0;

            foreach (var fieldDef in modelDef.FieldDefinitions)
            {
                if (fieldDef.ShouldSkipUpdate())
                    continue;

                try
                {
                    if (fieldDef.IsPrimaryKey && updateAllFields)
                    {
                        if (sqlFilter.Length > 0)
                            sqlFilter.Append(" AND ");

                        sqlFilter.Append(this.GetQuotedColumnName(fieldDef.FieldName)).Append("=").Append(
                            this.AddQueryParam(dbCmd, fieldDef.GetValue(objWithProperties), fieldDef).ParameterName);

                        continue;
                    }

                    if (!updateAllFields && !updateFields.Contains(fieldDef.Name, StringComparer.OrdinalIgnoreCase) ||
                        fieldDef.AutoIncrement)
                        continue;

                    if (sql.Length > 0)
                        sql.Append(", ");

                    sql.Append(this.GetQuotedColumnName(fieldDef.FieldName)).Append("=").Append(
                        this.GetUpdateParam(dbCmd, fieldDef.GetValue(objWithProperties), fieldDef));
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex, "ERROR in ToUpdateRowStatement(): " + ex.Message);
                }
            }

            var strFilter = StringBuilderCacheAlt.ReturnAndFree(sqlFilter);
            dbCmd.CommandText = $"UPDATE {this.GetQuotedTableName(modelDef)} " +
                                $"SET {StringBuilderCache.ReturnAndFree(sql)}{(strFilter.Length > 0 ? " WHERE " + strFilter : string.Empty)}";

            if (sql.Length == 0)
                throw new Exception(
                    "No valid update properties provided (e.g. p => p.FirstName): " + dbCmd.CommandText);
        }

        public virtual void PrepareUpdateRowStatement<T>(
            IDbCommand dbCmd,
            Dictionary<string, object> args,
            string sqlFilter)
        {
            var sql = StringBuilderCache.Allocate();
            var modelDef = typeof(T).GetModelDefinition();

            foreach (var entry in args)
            {
                var fieldDef = modelDef.AssertFieldDefinition(entry.Key);
                if (fieldDef.ShouldSkipUpdate() || fieldDef.IsPrimaryKey || fieldDef.AutoIncrement)
                    continue;

                var value = entry.Value;

                try
                {
                    if (sql.Length > 0)
                    {
                        sql.Append(", ");
                    }

                    sql.Append(this.GetQuotedColumnName(fieldDef.FieldName));

                    sql.Append("=");

                    sql.Append(this.GetUpdateParam(dbCmd, value, fieldDef));
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex, "ERROR in PrepareUpdateRowStatement(cmd,args): " + ex.Message);
                }
            }

            dbCmd.CommandText = $"UPDATE {this.GetQuotedTableName(modelDef)} " +
                                $"SET {StringBuilderCache.ReturnAndFree(sql)}{(string.IsNullOrEmpty(sqlFilter) ? string.Empty : " ")}{sqlFilter}";

            if (sql.Length == 0)
                throw new Exception(
                    "No valid update properties provided (e.g. () => new Person { Age = 27 }): " + dbCmd.CommandText);
        }

        public virtual void PrepareUpdateRowAddStatement<T>(
            IDbCommand dbCmd,
            Dictionary<string, object> args,
            string sqlFilter)
        {
            var sql = StringBuilderCache.Allocate();
            var modelDef = typeof(T).GetModelDefinition();

            foreach (var entry in args)
            {
                var fieldDef = modelDef.AssertFieldDefinition(entry.Key);
                if (fieldDef.ShouldSkipUpdate() || fieldDef.AutoIncrement || fieldDef.IsPrimaryKey ||
                    fieldDef.IsRowVersion || fieldDef.Name == OrmLiteConfig.IdField)
                    continue;

                var value = entry.Value;

                try
                {
                    if (sql.Length > 0)
                        sql.Append(", ");

                    var quotedFieldName = this.GetQuotedColumnName(fieldDef.FieldName);

                    if (fieldDef.FieldType.IsNumericType())
                    {
                        sql.Append(quotedFieldName).Append("=").Append(quotedFieldName).Append("+")
                            .Append(this.GetUpdateParam(dbCmd, value, fieldDef));
                    }
                    else
                    {
                        sql.Append(quotedFieldName).Append("=").Append(this.GetUpdateParam(dbCmd, value, fieldDef));
                    }
                }
                catch (Exception ex)
                {
                    OrmLiteUtils.HandleException(ex, "ERROR in PrepareUpdateRowAddStatement(): " + ex.Message);
                }
            }

            dbCmd.CommandText = $"UPDATE {this.GetQuotedTableName(modelDef)} " +
                                $"SET {StringBuilderCache.ReturnAndFree(sql)}{(string.IsNullOrEmpty(sqlFilter) ? string.Empty : " ")}{sqlFilter}";

            if (sql.Length == 0)
                throw new Exception(
                    "No valid update properties provided (e.g. () => new Person { Age = 27 }): " + dbCmd.CommandText);
        }

        public virtual string ToDeleteStatement(Type tableType, string sqlFilter, params object[] filterParams)
        {
            var sql = StringBuilderCache.Allocate();
            const string deleteStatement = "DELETE ";

            var isFullDeleteStatement = !string.IsNullOrEmpty(sqlFilter) && sqlFilter.Length > deleteStatement.Length &&
                                        sqlFilter.Substring(0, deleteStatement.Length).ToUpper()
                                            .Equals(deleteStatement);

            if (isFullDeleteStatement)
                return sqlFilter.SqlFmt(this, filterParams);

            var modelDef = tableType.GetModelDefinition();
            sql.Append($"DELETE FROM {this.GetQuotedTableName(modelDef)}");

            if (string.IsNullOrEmpty(sqlFilter))
                return StringBuilderCache.ReturnAndFree(sql);

            sqlFilter = sqlFilter.SqlFmt(this, filterParams);
            sql.Append(" WHERE ");
            sql.Append(sqlFilter);

            return StringBuilderCache.ReturnAndFree(sql);
        }

        public virtual bool HasInsertReturnValues(ModelDefinition modelDef) =>
            modelDef.FieldDefinitions.Any(x => x.ReturnOnInsert);

        public string GetDefaultValue(Type tableType, string fieldName)
        {
            var modelDef = tableType.GetModelDefinition();
            var fieldDef = modelDef.AssertFieldDefinition(fieldName);
            return this.GetDefaultValue(fieldDef);
        }

        public virtual string GetDefaultValue(FieldDefinition fieldDef)
        {
            var defaultValue = fieldDef.DefaultValue;
            if (string.IsNullOrEmpty(defaultValue))
            {
                return fieldDef.AutoId ? this.GetAutoIdDefaultValue(fieldDef) : null;
            }

            return this.ResolveFragment(defaultValue);
        }

        public virtual string ResolveFragment(string sql)
        {
            if (string.IsNullOrEmpty(sql))
                return null;

            if (!sql.StartsWith("{"))
                return sql;

            return this.Variables.TryGetValue(sql, out var variable) ? variable : null;
        }

        public virtual string GetAutoIdDefaultValue(FieldDefinition fieldDef) => null;

        public Func<ModelDefinition, List<FieldDefinition>> CreateTableFieldsStrategy { get; set; } =
            GetFieldDefinitions;

        public static List<FieldDefinition> GetFieldDefinitions(ModelDefinition modelDef) => modelDef.FieldDefinitions;

        public abstract string ToCreateSchemaStatement(string schemaName);

        public abstract bool DoesSchemaExist(IDbCommand dbCmd, string schemaName);

        public virtual Task<bool> DoesSchemaExistAsync(
            IDbCommand dbCmd,
            string schema,
            CancellationToken token = default)
        {
            return this.DoesSchemaExist(dbCmd, schema).InTask();
        }

        public virtual string ToCreateTableStatement(Type tableType)
        {
            var sbColumns = StringBuilderCache.Allocate();
            var sbConstraints = StringBuilderCacheAlt.Allocate();

            var modelDef = tableType.GetModelDefinition();
            foreach (var fieldDef in this.CreateTableFieldsStrategy(modelDef))
            {
                if (fieldDef.CustomSelect != null || fieldDef.IsComputed && !fieldDef.IsPersisted)
                    continue;

                var columnDefinition = this.GetColumnDefinition(fieldDef, modelDef);

                if (columnDefinition == null)
                    continue;

                if (sbColumns.Length != 0)
                    sbColumns.Append(", \n  ");

                sbColumns.Append(columnDefinition);

                var sqlConstraint = this.GetCheckConstraint(modelDef, fieldDef);
                if (sqlConstraint != null)
                {
                    sbConstraints.Append(",\n" + sqlConstraint);
                }

                if (fieldDef.ForeignKey == null || OrmLiteConfig.SkipForeignKeys)
                    continue;

                var refModelDef = fieldDef.ForeignKey.ReferenceType.GetModelDefinition();
                sbConstraints.Append(
                    $", \n\n  CONSTRAINT {this.GetQuotedName(fieldDef.ForeignKey.GetForeignKeyName(modelDef, refModelDef, this.NamingStrategy, fieldDef))} " +
                    $"FOREIGN KEY ({this.GetQuotedColumnName(fieldDef.FieldName)}) " +
                    $"REFERENCES {this.GetQuotedTableName(refModelDef)} ({this.GetQuotedColumnName(refModelDef.PrimaryKey.FieldName)})");

                sbConstraints.Append(this.GetForeignKeyOnDeleteClause(fieldDef.ForeignKey));
                sbConstraints.Append(this.GetForeignKeyOnUpdateClause(fieldDef.ForeignKey));
            }

            var uniqueConstraints = this.GetUniqueConstraints(modelDef);
            if (uniqueConstraints != null)
            {
                sbConstraints.Append(",\n" + uniqueConstraints);
            }

            /*var compositePrimaryKey = GetCompositePrimaryKey(modelDef);
            if (compositePrimaryKey != null)
            {
                sbConstraints.Append(",\n" + compositePrimaryKey);
            }*/

            var sql = $"CREATE TABLE {this.GetQuotedTableName(modelDef)} " +
                      $"\n(\n  {StringBuilderCache.ReturnAndFree(sbColumns)}{StringBuilderCacheAlt.ReturnAndFree(sbConstraints)} \n); \n";

            return sql;
        }

        public virtual string GetUniqueConstraints(ModelDefinition modelDef)
        {
            var constraints = modelDef.UniqueConstraints.Map(
                x =>
                    $"CONSTRAINT {this.GetUniqueConstraintName(x, this.GetTableName(modelDef).StripDbQuotes())} UNIQUE ({x.FieldNames.Map(f => modelDef.GetQuotedName(f, this)).Join(",")})");

            return constraints.Count > 0 ? constraints.Join(",\n") : null;
        }

        protected virtual string GetUniqueConstraintName(UniqueConstraintAttribute constraint, string tableName) =>
            constraint.Name ?? $"UC_{tableName}_{constraint.FieldNames.Join("_")}";

        public virtual string GetCheckConstraint(ModelDefinition modelDef, FieldDefinition fieldDef)
        {
            if (fieldDef.CheckConstraint == null)
                return null;

            return
                $"CONSTRAINT CHK_{modelDef.Schema}_{modelDef.ModelName}_{fieldDef.FieldName} CHECK ({fieldDef.CheckConstraint})";
        }

        public virtual string ToPostCreateTableStatement(ModelDefinition modelDef)
        {
            return null;
        }

        public virtual string ToPostDropTableStatement(ModelDefinition modelDef)
        {
            return null;
        }

        public virtual string GetForeignKeyOnDeleteClause(ForeignKeyConstraint foreignKey)
        {
            return !string.IsNullOrEmpty(foreignKey.OnDelete) ? " ON DELETE " + foreignKey.OnDelete : string.Empty;
        }

        public virtual string GetForeignKeyOnUpdateClause(ForeignKeyConstraint foreignKey)
        {
            return !string.IsNullOrEmpty(foreignKey.OnUpdate) ? " ON UPDATE " + foreignKey.OnUpdate : string.Empty;
        }

        public virtual List<string> ToCreateIndexStatements(Type tableType)
        {
            var sqlIndexes = new List<string>();

            var modelDef = tableType.GetModelDefinition();
            foreach (var fieldDef in modelDef.FieldDefinitions)
            {
                if (!fieldDef.IsIndexed) continue;

                var indexName = fieldDef.IndexName ?? this.GetIndexName(
                    fieldDef.IsUniqueIndex,
                    modelDef.ModelName.SafeVarName(),
                    fieldDef.FieldName);

                sqlIndexes.Add(
                    this.ToCreateIndexStatement(
                        fieldDef.IsUniqueIndex,
                        indexName,
                        modelDef,
                        fieldDef.FieldName,
                        isCombined: false,
                        fieldDef: fieldDef));
            }

            foreach (var compositeIndex in modelDef.CompositeIndexes)
            {
                var indexName = this.GetCompositeIndexName(compositeIndex, modelDef);

                var sb = StringBuilderCache.Allocate();
                foreach (var fieldName in compositeIndex.FieldNames)
                {
                    if (sb.Length > 0)
                        sb.Append(", ");

                    var parts = fieldName.SplitOnLast(' ');
                    if (parts.Length == 2 &&
                        (parts[1].ToLower().StartsWith("desc") || parts[1].ToLower().StartsWith("asc")))
                    {
                        sb.Append(this.GetQuotedColumnName(parts[0])).Append(' ').Append(parts[1]);
                    }
                    else
                    {
                        sb.Append(this.GetQuotedColumnName(fieldName));
                    }
                }

                sqlIndexes.Add(
                    this.ToCreateIndexStatement(
                        compositeIndex.Unique,
                        indexName,
                        modelDef,
                        StringBuilderCache.ReturnAndFree(sb),
                        isCombined: true));
            }

            return sqlIndexes;
        }

        public virtual bool DoesTableExist(IDbConnection db, string tableName, string schema = null)
        {
            return db.Exec(dbCmd => this.DoesTableExist(dbCmd, tableName, schema));
        }

        public virtual async Task<bool> DoesTableExistAsync(
            IDbConnection db,
            string tableName,
            string schema = null,
            CancellationToken token = default)
        {
            return await db.Exec(async dbCmd => await this.DoesTableExistAsync(dbCmd, tableName, schema, token));
        }

        public virtual bool DoesTableExist(IDbCommand dbCmd, string tableName, string schema = null)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> DoesTableExistAsync(
            IDbCommand dbCmd,
            string tableName,
            string schema = null,
            CancellationToken token = default)
        {
            return this.DoesTableExist(dbCmd, tableName, schema).InTask();
        }

        public virtual bool DoesColumnExist(IDbConnection db, string columnName, string tableName, string schema = null)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> DoesColumnExistAsync(
            IDbConnection db,
            string columnName,
            string tableName,
            string schema = null,
            CancellationToken token = default)
        {
            return this.DoesColumnExist(db, columnName, tableName, schema).InTask();
        }

        public virtual string GetColumnDataType(
            IDbConnection db,
            string columnName,
            string tableName,
            string schema = null)
        {
            throw new NotImplementedException();
        }

        public virtual bool ColumnIsNullable(
            IDbConnection db,
            string columnName,
            string tableName,
            string schema = null)
        {
            throw new NotImplementedException();
        }

        public virtual long GetColumnMaxLength(
            IDbConnection db,
            string columnName,
            string tableName,
            string schema = null)
        {
            throw new NotImplementedException();
        }

        public virtual bool DoesSequenceExist(IDbCommand dbCmd, string sequenceName)
        {
            throw new NotImplementedException();
        }

        public virtual Task<bool> DoesSequenceExistAsync(
            IDbCommand dbCmd,
            string sequenceName,
            CancellationToken token = default)
        {
            return this.DoesSequenceExist(dbCmd, sequenceName).InTask();
        }

        protected virtual string GetIndexName(bool isUnique, string modelName, string fieldName)
        {
            return $"{(isUnique ? "u" : string.Empty)}idx_{modelName}_{fieldName}".ToLower();
        }

        protected virtual string GetCompositeIndexName(CompositeIndexAttribute compositeIndex, ModelDefinition modelDef)
        {
            return compositeIndex.Name ?? this.GetIndexName(
                compositeIndex.Unique,
                modelDef.ModelName.SafeVarName(),
                string.Join("_", compositeIndex.FieldNames.Map(x => x.LeftPart(' ')).ToArray()));
        }

        protected virtual string GetCompositeIndexNameWithSchema(
            CompositeIndexAttribute compositeIndex,
            ModelDefinition modelDef)
        {
            return compositeIndex.Name ?? this.GetIndexName(
                compositeIndex.Unique,
                (modelDef.IsInSchema
                    ? modelDef.Schema + "_" + this.GetQuotedTableName(modelDef)
                    : this.GetQuotedTableName(modelDef)).SafeVarName(),
                string.Join("_", compositeIndex.FieldNames.ToArray()));
        }

        protected virtual string ToCreateIndexStatement(
            bool isUnique,
            string indexName,
            ModelDefinition modelDef,
            string fieldName,
            bool isCombined = false,
            FieldDefinition fieldDef = null)
        {
            return $"CREATE {(isUnique ? "UNIQUE" : string.Empty)}" +
                   (fieldDef?.IsClustered == true ? " CLUSTERED" : string.Empty) +
                   (fieldDef?.IsNonClustered == true ? " NONCLUSTERED" : string.Empty) +
                   $" INDEX {indexName} ON {this.GetQuotedTableName(modelDef)} " +
                   $"({(isCombined ? fieldName : this.GetQuotedColumnName(fieldName))}); \n";
        }

        public virtual List<string> ToCreateSequenceStatements(Type tableType)
        {
            return new List<string>();
        }

        public virtual string ToCreateSequenceStatement(Type tableType, string sequenceName)
        {
            return string.Empty;
        }

        public virtual List<string> SequenceList(Type tableType) => new List<string>();

        public virtual Task<List<string>> SequenceListAsync(Type tableType, CancellationToken token = default) =>
            new List<string>().InTask();

        // TODO : make abstract  ??
        public virtual string ToExistStatement(
            Type fromTableType,
            object objWithProperties,
            string sqlFilter,
            params object[] filterParams)
        {
            throw new NotImplementedException();
        }

        // TODO : make abstract  ??
        public virtual string ToSelectFromProcedureStatement(
            object fromObjWithProperties,
            Type outputModelType,
            string sqlFilter,
            params object[] filterParams)
        {
            throw new NotImplementedException();
        }

        // TODO : make abstract  ??
        public virtual string ToExecuteProcedureStatement(object objWithProperties)
        {
            return null;
        }

        protected static ModelDefinition GetModel(Type modelType)
        {
            return modelType.GetModelDefinition();
        }

        public virtual SqlExpression<T> SqlExpression<T>()
        {
            throw new NotImplementedException();
        }

        public IDbCommand CreateParameterizedDeleteStatement(IDbConnection connection, object objWithProperties)
        {
            throw new NotImplementedException();
        }

        public virtual string GetFunctionName(string database, string functionName)
        {
            return null;
        }

        public virtual string GetDropFunction(string database, string functionName)
        {
            return null;
        }

        public virtual string GetCreateView(string database, ModelDefinition modelDef, StringBuilder selectSql)
        {
            return null;
        }

        public virtual string GetDropView(string database, ModelDefinition modelDef)
        {
            return null;
        }

        public virtual string GetCreateIndexView(ModelDefinition modelDef, string name, string selectSql)
        {
            return null;
        }

        public virtual string GetDropIndexView(ModelDefinition modelDef, string name)
        {
            return null;
        }

        public virtual string GetDropIndexConstraint(ModelDefinition modelDef, string name = null)
        {
            return null;
        }

        public virtual string GetDropPrimaryKeyConstraint(ModelDefinition modelDef, string name)
        {
            return null;
        }

        public virtual string GetDropForeignKeyConstraint(ModelDefinition modelDef, string name)
        {
            return null;
        }

        public virtual string GetDropForeignKeyConstraints(ModelDefinition modelDef)
        {
            return null;
        }

        public virtual string ToAddColumnStatement(Type modelType, FieldDefinition fieldDef)
        {
            var column = this.GetColumnDefinition(fieldDef);
            return $"ALTER TABLE {this.GetQuotedTableName(modelType.GetModelDefinition())} ADD COLUMN {column};";
        }

        public virtual string ToAlterColumnStatement(Type modelType, FieldDefinition fieldDef)
        {
            var column = this.GetColumnDefinition(fieldDef);
            return $"ALTER TABLE {this.GetQuotedTableName(modelType.GetModelDefinition())} MODIFY COLUMN {column};";
        }

        public virtual string ToChangeColumnNameStatement(
            Type modelType,
            FieldDefinition fieldDef,
            string oldColumnName)
        {
            var column = this.GetColumnDefinition(fieldDef);
            return
                $"ALTER TABLE {this.GetQuotedTableName(modelType.GetModelDefinition())} CHANGE COLUMN {this.GetQuotedColumnName(oldColumnName)} {column};";
        }

        public virtual string ToAddForeignKeyStatement<T, TForeign>(
            Expression<Func<T, object>> field,
            Expression<Func<TForeign, object>> foreignField,
            OnFkOption onUpdate,
            OnFkOption onDelete,
            string foreignKeyName = null)
        {
            var sourceMD = ModelDefinition<T>.Definition;
            var fieldName = sourceMD.GetFieldDefinition(field).FieldName;

            var referenceMD = ModelDefinition<TForeign>.Definition;
            var referenceFieldName = referenceMD.GetFieldDefinition(foreignField).FieldName;

            string name = this.GetQuotedName(
                foreignKeyName.IsNullOrEmpty()
                    ? "fk_" + sourceMD.ModelName + "_" + fieldName + "_" + referenceFieldName
                    : foreignKeyName);

            return $"ALTER TABLE {this.GetQuotedTableName(sourceMD)} " +
                   $"ADD CONSTRAINT {name} FOREIGN KEY ({this.GetQuotedColumnName(fieldName)}) " +
                   $"REFERENCES {this.GetQuotedTableName(referenceMD)} " +
                   $"({this.GetQuotedColumnName(referenceFieldName)})" +
                   $"{this.GetForeignKeyOnDeleteClause(new ForeignKeyConstraint(typeof(T), onDelete: this.FkOptionToString(onDelete)))}" +
                   $"{this.GetForeignKeyOnUpdateClause(new ForeignKeyConstraint(typeof(T), onUpdate: this.FkOptionToString(onUpdate)))};";
        }

        public virtual string ToCreateIndexStatement<T>(
            Expression<Func<T, object>> field,
            string indexName = null,
            bool unique = false)
        {
            var sourceDef = ModelDefinition<T>.Definition;
            var fieldName = sourceDef.GetFieldDefinition(field).FieldName;

            string name = this.GetQuotedName(
                indexName.IsNullOrEmpty()
                    ? (unique ? "uidx" : "idx") + "_" + sourceDef.ModelName + "_" + fieldName
                    : indexName);

            string command = $"CREATE {(unique ? "UNIQUE" : string.Empty)} " +
                             $"INDEX {name} ON {this.GetQuotedTableName(sourceDef)}" +
                             $"({this.GetQuotedColumnName(fieldName)});";
            return command;
        }

        protected virtual string FkOptionToString(OnFkOption option)
        {
            switch (option)
            {
                case OnFkOption.Cascade: return "CASCADE";
                case OnFkOption.NoAction: return "NO ACTION";
                case OnFkOption.SetNull: return "SET NULL";
                case OnFkOption.SetDefault: return "SET DEFAULT";
                case OnFkOption.Restrict:
                default: return "RESTRICT";
            }
        }

        public virtual string GetQuotedValue(object value, Type fieldType)
        {
            if (value == null || value == DBNull.Value)
                return "NULL";

            var converter = value.GetType().IsEnum ? this.EnumConverter : this.GetConverterBestMatch(fieldType);

            try
            {
                return converter.ToQuotedString(fieldType, value);
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Error in {converter.GetType().Name}.ToQuotedString() value '{converter.GetType().Name}' and Type '{value.GetType().Name}'",
                    ex);
                throw;
            }
        }

        public virtual object GetParamValue(object value, Type fieldType)
        {
            return this.ToDbValue(value, fieldType);
        }

        public virtual void InitQueryParam(IDbDataParameter param)
        {
        }

        public virtual void InitUpdateParam(IDbDataParameter param)
        {
        }

        public virtual string EscapeWildcards(string value)
        {
            return value?.Replace("^", @"^^").Replace(@"\", @"^\").Replace("_", @"^_").Replace("%", @"^%");
        }

        public virtual string GetLoadChildrenSubSelect<From>(SqlExpression<From> expr)
        {
            var modelDef = expr.ModelDef;
            expr.UnsafeSelect(this.GetQuotedColumnName(modelDef, modelDef.PrimaryKey));

            var subSql = expr.ToSelectStatement();

            return subSql;
        }

        public virtual string ToRowCountStatement(string innerSql)
        {
            return $"SELECT COUNT(*) FROM ({innerSql}) AS COUNT";
        }

        public virtual void DropColumn(IDbConnection db, Type modelType, string columnName)
        {
            var provider = db.GetDialectProvider();
            var command = this.ToDropColumnStatement(modelType, columnName, provider);

            db.ExecuteSql(command);
        }

        protected virtual string ToDropColumnStatement(
            Type modelType,
            string columnName,
            IOrmLiteDialectProvider provider)
        {
            return $"ALTER TABLE {provider.GetQuotedTableName(modelType.GetModelDefinition())} " +
                   $"DROP COLUMN {provider.GetQuotedColumnName(columnName)};";
        }

        public virtual string ToTableNamesStatement(string schema) => throw new NotSupportedException();

        public virtual string ToTableNamesWithRowCountsStatement(bool live, string schema) =>
            null; // returning null Fallsback to slow UNION N+1 COUNT(*) op

        public virtual string SqlConflict(string sql, string conflictResolution) => sql; // NOOP

        public virtual string SqlConcat(IEnumerable<object> args) => $"CONCAT({string.Join(", ", args)})";

        public virtual string SqlCurrency(string fieldOrValue) => this.SqlCurrency(fieldOrValue, "$");

        public virtual string SqlCurrency(string fieldOrValue, string currencySymbol) =>
            this.SqlConcat(new List<string> { currencySymbol, fieldOrValue });

        public virtual string SqlBool(bool value) => value ? "true" : "false";

        public virtual string SqlLimit(int? offset = null, int? rows = null) =>
            rows == null && offset == null ? string.Empty :
            offset == null ? "LIMIT " + rows : "LIMIT " + rows.GetValueOrDefault(int.MaxValue) + " OFFSET " + offset;

        public virtual string SqlCast(object fieldOrValue, string castAs) => $"CAST({fieldOrValue} AS {castAs})";

        public virtual string SqlRandom => "RAND()";

        // Async API's, should be overriden by Dialect Providers to use .ConfigureAwait(false)
        // Default impl below uses TaskAwaiter shim in async.cs
        public virtual Task OpenAsync(IDbConnection db, CancellationToken token = default)
        {
            db.Open();
            return TaskResult.Finished;
        }

        public virtual Task<IDataReader> ExecuteReaderAsync(IDbCommand cmd, CancellationToken token = default)
        {
            return cmd.ExecuteReader().InTask();
        }

        public virtual Task<int> ExecuteNonQueryAsync(IDbCommand cmd, CancellationToken token = default)
        {
            return cmd.ExecuteNonQuery().InTask();
        }

        public virtual Task<object> ExecuteScalarAsync(IDbCommand cmd, CancellationToken token = default)
        {
            return cmd.ExecuteScalar().InTask();
        }

        public virtual Task<bool> ReadAsync(IDataReader reader, CancellationToken token = default)
        {
            return reader.Read().InTask();
        }

#if ASYNC
        public virtual async Task<List<T>> ReaderEach<T>(
            IDataReader reader,
            Func<T> fn,
            CancellationToken token = default)
        {
            try
            {
                var to = new List<T>();
                while (await this.ReadAsync(reader, token))
                {
                    var row = fn();
                    to.Add(row);
                }

                return to;
            }
            finally
            {
                reader.Dispose();
            }
        }

        public virtual async Task<Return> ReaderEach<Return>(
            IDataReader reader,
            Action fn,
            Return source,
            CancellationToken token = default)
        {
            try
            {
                while (await this.ReadAsync(reader, token))
                {
                    fn();
                }

                return source;
            }
            finally
            {
                reader.Dispose();
            }
        }

        public virtual async Task<T> ReaderRead<T>(IDataReader reader, Func<T> fn, CancellationToken token = default)
        {
            try
            {
                if (await this.ReadAsync(reader, token))
                    return fn();

                return default(T);
            }
            finally
            {
                reader.Dispose();
            }
        }

        public virtual Task<long> InsertAndGetLastInsertIdAsync<T>(IDbCommand dbCmd, CancellationToken token)
        {
            if (this.SelectIdentitySql == null)
                return new NotImplementedException(
                    "Returning last inserted identity is not implemented on this DB Provider.").InTask<long>();

            dbCmd.CommandText += "; " + this.SelectIdentitySql;

            return dbCmd.ExecLongScalarAsync(null, token);
        }

#else
        public Task<List<T>> ReaderEach<T>(IDataReader reader, Func<T> fn, CancellationToken token =
 new CancellationToken())
        {
            throw new NotImplementedException(OrmLiteUtils.AsyncRequiresNet45Error);
        }

        public Task<Return> ReaderEach<Return>(IDataReader reader, Action fn, Return source, CancellationToken token =
 new CancellationToken())
        {
            throw new NotImplementedException(OrmLiteUtils.AsyncRequiresNet45Error);
        }

        public Task<T> ReaderRead<T>(IDataReader reader, Func<T> fn, CancellationToken token = new CancellationToken())
        {
            throw new NotImplementedException(OrmLiteUtils.AsyncRequiresNet45Error);
        }

        public Task<long> InsertAndGetLastInsertIdAsync<T>(IDbCommand dbCmd, CancellationToken token)
        {
            throw new NotImplementedException(OrmLiteUtils.AsyncRequiresNet45Error);
        }
#endif

        public virtual string GetUtcDateFunction()
        {
            throw new NotImplementedException();
        }

        public virtual string DateDiffFunction(string interval, string date1, string date2)
        {
            throw new NotImplementedException();
        }

        public virtual string IsNullFunction(string expression, object alternateValue)
        {
            throw new NotImplementedException();
        }

        public virtual string ConvertFlag(string expression)
        {
            throw new NotImplementedException();
        }

        public virtual string DatabaseFragmentationInfo(string database)
        {
            throw new NotImplementedException();
        }

        public virtual string DatabaseSize(string database)
        {
            throw new NotImplementedException();
        }

        public virtual string SQLVersion()
        {
            throw new NotImplementedException();
        }

        public virtual string ShrinkDatabase(string database)
        {
            throw new NotImplementedException();
        }

        public virtual string ReIndexDatabase(string database, string objectQualifier)
        {
            throw new NotImplementedException();
        }

        public virtual string ChangeRecoveryMode(string database, string mode)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Just runs the SQL command according to specifications.
        /// </summary>
        /// <param name="command">
        /// The command.
        /// </param>
        /// <returns>
        /// Returns the Results
        /// </returns>
        public virtual string InnerRunSqlExecuteReader(IDbCommand command)
        {
            throw new NotImplementedException();
        }
    }
}
