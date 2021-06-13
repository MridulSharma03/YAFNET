namespace ServiceStack.OrmLite
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Threading.Tasks;

    public interface IOrmLiteExecFilter
    {
        SqlExpression<T> SqlExpression<T>(IDbConnection dbConn);
        IDbCommand CreateCommand(IDbConnection dbConn);
        void DisposeCommand(IDbCommand dbCmd, IDbConnection dbConn);
        T Exec<T>(IDbConnection dbConn, Func<IDbCommand, T> filter);
        IDbCommand Exec(IDbConnection dbConn, Func<IDbCommand, IDbCommand> filter);
        Task<T> Exec<T>(IDbConnection dbConn, Func<IDbCommand, Task<T>> filter);
        Task<IDbCommand> Exec(IDbConnection dbConn, Func<IDbCommand, Task<IDbCommand>> filter);
        void Exec(IDbConnection dbConn, Action<IDbCommand> filter);
        Task Exec(IDbConnection dbConn, Func<IDbCommand, Task> filter);
        IEnumerable<T> ExecLazy<T>(IDbConnection dbConn, Func<IDbCommand, IEnumerable<T>> filter);
    }

    public class OrmLiteExecFilter : IOrmLiteExecFilter
    {
        public virtual SqlExpression<T> SqlExpression<T>(IDbConnection dbConn)
        {
            return dbConn.GetDialectProvider().SqlExpression<T>();
        }

        public virtual IDbCommand CreateCommand(IDbConnection dbConn)
        {
            var ormLiteConn = dbConn as OrmLiteConnection;

            var dbCmd = dbConn.CreateCommand();

            dbCmd.Transaction = ormLiteConn != null
                ? ormLiteConn.Transaction
                : OrmLiteContext.TSTransaction;

            dbCmd.CommandTimeout = ormLiteConn != null
                ? ormLiteConn.CommandTimeout ?? OrmLiteConfig.CommandTimeout
                : OrmLiteConfig.CommandTimeout;

            ormLiteConn.SetLastCommandText(null);

            return new OrmLiteCommand(ormLiteConn, dbCmd);
        }

        public virtual void DisposeCommand(IDbCommand dbCmd, IDbConnection dbConn)
        {
            if (dbCmd == null)
            {
                return;
            }

            OrmLiteConfig.AfterExecFilter?.Invoke(dbCmd);

            dbConn.SetLastCommandText(dbCmd.CommandText);

            dbCmd.Dispose();
        }

        public virtual T Exec<T>(IDbConnection dbConn, Func<IDbCommand, T> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);

            try
            {
                var ret = filter(dbCmd);
                return ret;
            }
            catch (Exception ex)
            {
                OrmLiteConfig.ExceptionFilter?.Invoke(dbCmd, ex);
                       throw;
            }
            finally
            {
                this.DisposeCommand(dbCmd, dbConn);
            }
        }

        public virtual IDbCommand Exec(IDbConnection dbConn, Func<IDbCommand, IDbCommand> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);
            var ret = filter(dbCmd);
            if (dbCmd != null)
            {
                dbConn.SetLastCommandText(dbCmd.CommandText);
            }

            return ret;
        }

        public virtual void Exec(IDbConnection dbConn, Action<IDbCommand> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);
            try
            {
                filter(dbCmd);
            }
            catch (Exception ex)
            {
                OrmLiteConfig.ExceptionFilter?.Invoke(dbCmd, ex);
                throw;
            }
            finally
            {
                this.DisposeCommand(dbCmd, dbConn);
            }
        }

        public virtual async Task<T> Exec<T>(IDbConnection dbConn, Func<IDbCommand, Task<T>> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);

            try
            {
                return await filter(dbCmd);
            }
            catch (Exception ex)
            {
                var useEx = ex.UnwrapIfSingleException();
                OrmLiteConfig.ExceptionFilter?.Invoke(dbCmd, useEx);
                throw useEx;
            }
            finally
            {
                this.DisposeCommand(dbCmd, dbConn);
            }
        }

        public virtual async Task<IDbCommand> Exec(IDbConnection dbConn, Func<IDbCommand, Task<IDbCommand>> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);
            return await filter(dbCmd);
        }

        public virtual async Task Exec(IDbConnection dbConn, Func<IDbCommand, Task> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);

            try
            {
                await filter(dbCmd);
            }
            catch (Exception ex)
            {
                var useEx = ex.UnwrapIfSingleException();
                OrmLiteConfig.ExceptionFilter?.Invoke(dbCmd, useEx);
                throw useEx;
            }
            finally
            {
                this.DisposeCommand(dbCmd, dbConn);
            }
        }

        public virtual IEnumerable<T> ExecLazy<T>(IDbConnection dbConn, Func<IDbCommand, IEnumerable<T>> filter)
        {
            var dbCmd = this.CreateCommand(dbConn);
            try
            {
                var results = filter(dbCmd);

                foreach (var item in results)
                {
                    yield return item;
                }
            }
            finally
            {
                this.DisposeCommand(dbCmd, dbConn);
            }
        }
    }
}