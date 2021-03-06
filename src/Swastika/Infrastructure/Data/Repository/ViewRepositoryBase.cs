﻿using AutoMapper;
using Microsoft.Data.OData.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Swastika.Common.Helper;
using Swastika.Domain.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Swastika.Infrastructure.Data.Repository
{
    /// <summary>
    /// Base Repository
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <typeparam name="TView">The type of the view.</typeparam>
    /// <typeparam name="TDbContext">The type of the context.</typeparam>
    /// <seealso cref="Swastika.Extension.Blog.Interfaces.IRepository{TModel, TView}" />
    public abstract class ViewRepositoryBase<TDbContext, TModel, TView>
       where TModel : class
        where TView : Swastika.Infrastructure.Data.ViewModels.ViewModelBase<TDbContext, TModel, TView>
        where TDbContext : DbContext
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="SWBaseRepository{TModel, TView, TContext}"/> class.
        /// </summary>
        public ViewRepositoryBase()
        {
            RegisterAutoMapper();
        }

        /// <summary>
        /// Initializes the context.
        /// </summary>
        /// <returns></returns>
        public virtual TDbContext InitContext()
        {
            Type classType = typeof(TDbContext);
            ConstructorInfo classConstructor = classType.GetConstructor(new Type[] { });
            TDbContext context = (TDbContext)classConstructor.Invoke(new object[] { });

            return context;
        }

        /// <summary>
        /// Registers the automatic mapper.
        /// </summary>
        public virtual void RegisterAutoMapper()
        {
            Mapper.Initialize(cfg =>
            {
                cfg.CreateMap<TModel, TView>();
                cfg.CreateMap<TView, TModel>();
            });
        }

        /// <summary>
        /// Parses the view.
        /// </summary>
        /// <param name="lstModels">The LST Items.</param>
        /// <returns></returns>
        public virtual List<TView> ParseView(List<TModel> lstModels, TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            List<TView> lstView = new List<TView>();
            foreach (var model in lstModels)
            {
                lstView.Add(ParseView(model, _context, _transaction));
            }

            return lstView;
        }

        /// <summary>
        /// Parses the view.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual TView ParseView(TModel model, TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            Type classType = typeof(TView);
            ConstructorInfo classConstructor = classType.GetConstructor(new Type[] { model.GetType(), typeof(TDbContext), typeof(IDbContextTransaction) });
            TView vm = default(TView);
            if (classConstructor != null)
            {
                vm = (TView)classConstructor.Invoke(new object[] { model, _context, _transaction });

            }
            else
            {
                classConstructor = classType.GetConstructor(new Type[] { model.GetType() });
                vm = (TView)classConstructor.Invoke(new object[] { model });
            }

            return vm;
        }

        /// <summary>
        /// Determines whether the specified entity is exists.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>
        ///   <c>true</c> if the specified entity is exists; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool CheckIsExists(TModel entity, TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                //For the former case use:
                return context.Set<TModel>().Any(e => e == entity);

                //For the latter case use(it will check loaded entities as well):
                //return (_context.Set<T>().Find(keys) != null);
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    transaction.Rollback();
                }
                return false;
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    transaction.Dispose();
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Determines whether the specified predicate is exists.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>
        ///   <c>true</c> if the specified predicate is exists; otherwise, <c>false</c>.
        /// </returns>
        public bool CheckIsExists(System.Func<TModel, bool> predicate, TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                //For the former case use:
                return context.Set<TModel>().Any(predicate);

                //For the latter case use(it will check loaded entities as well):
                //return (_context.Set<T>().Find(keys) != null);
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    transaction.Rollback();
                }
                return false;
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    transaction.Dispose();
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates the model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<TView> CreateModel(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {

                context.Entry(view.Model).State = EntityState.Added;
                bool result = context.SaveChanges() > 0;
                if (result && isSaveSubModels)
                {
                    var saveResult = view.SaveSubModels(view.Model, context, transaction);
                    if (!saveResult.IsSucceed)
                    {
                        view.Errors.AddRange(saveResult.Errors);
                    }
                    result = saveResult.IsSucceed;
                }
                if (result)
                {

                    if (_transaction == null)
                    {
                        transaction.Commit();
                    }

                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = view
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        transaction.Rollback();
                    }

                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = view
                    };
                }

            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    transaction.Rollback();
                }
                return new RepositoryResponse<TView>()
                {
                    IsSucceed = false,
                    Data = default(TView),
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    transaction.Dispose();
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates the model asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<TView>> CreateModelAsync(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                context.Entry(view.Model).State = EntityState.Added;
                bool result = await context.SaveChangesAsync() > 0;
                if (result && isSaveSubModels)
                {
                    var saveResult = await view.SaveSubModelsAsync(view.Model, context, transaction);
                    result = saveResult.IsSucceed;
                }
                if (result)
                {
                    //var data = ParseView(view.Model, context, transaction);

                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }

                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = view //ParseView(view.Model, context, transaction)
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = view
                    };
                }


            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<TView>()
                {
                    IsSucceed = false,
                    Data = default(TView)
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    transaction.Dispose();
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Edits the model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<TView> EditModel(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                bool result = true;
                //context.Entry(view.Model).State = EntityState.Modified;
                context.Set<TModel>().Update(view.Model);
                context.SaveChanges();
                if (result && isSaveSubModels)
                {
                    var saveResult = view.SaveSubModels(view.Model, context, transaction);
                    if (!saveResult.IsSucceed)
                    {
                        view.Errors.AddRange(saveResult.Errors);
                    }
                    result = saveResult.IsSucceed;
                }
                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = view // ParseView(view.Model, context, transaction)
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = default(TView)
                    };
                }


            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }
                return new RepositoryResponse<TView>()
                {
                    IsSucceed = false,
                    Data = default(TView),
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    transaction.Dispose();
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Edits the model asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<TView>> EditModelAsync(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                bool result = true;
                //context.Entry(view.Model).State = EntityState.Modified;
                context.Set<TModel>().Update(view.Model);
                context.SaveChanges();
                if (result && isSaveSubModels)
                {
                    var saveResult = await view.SaveSubModelsAsync(view.Model, context, transaction);
                    if (!saveResult.IsSucceed)
                    {
                        view.Errors.AddRange(saveResult.Errors);
                    }
                    result = saveResult.IsSucceed;
                }
                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = view//ParseView(view.Model, context, transaction)
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = view
                    };
                }
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<TView>()
                {
                    IsSucceed = false,
                    Data = default(TView),
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        #region GetModelList

       

        /// <summary>
        /// Gets the model list.
        /// </summary>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual RepositoryResponse<List<TView>> GetModelList(TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            List<TView> result = new List<TView>();
            try
            {
                var lstModel = context.Set<TModel>().ToList();

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                result = ParseView(lstModel, _context, _transaction);
                return new RepositoryResponse<List<TView>>() {
                    IsSucceed = true,
                    Data = result
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<List<TView>>() {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the model list.
        /// </summary>
        /// <param name="orderBy">The order by.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual RepositoryResponse<PaginationModel<TView>> GetModelList(
            string orderByPropertyName, OrderByDirection direction, int? pageSize, int? pageIndex
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                dynamic orderBy = GetLambda(orderByPropertyName);
                IQueryable<TModel> sorted = null;
                List<TModel> lstModel = new List<TModel>();
                var query = context.Set<TModel>();

                PaginationModel<TView> result = new PaginationModel<TView>()
                {
                    TotalItems = query.Count(),
                    PageIndex = pageIndex ?? 0
                };
                result.PageSize = pageSize ?? result.TotalItems;

                if (pageSize.HasValue)
                {
                    result.TotalPage = result.TotalItems / pageSize.Value + (result.TotalItems % pageSize.Value > 0 ? 1 : 0);
                }

                // TODO: should we change "direction" to boolean "isDesc" and use if condition instead?
                switch (direction)
                {
                    case OrderByDirection.Descending:
                        sorted = Queryable.OrderByDescending(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToList();
                        }
                        else
                        {
                            lstModel = sorted.ToList();
                        }
                        break;

                    default:
                        sorted = Queryable.OrderBy(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToList();
                        }
                        else
                        {
                            lstModel = sorted.ToList();
                        }
                        break;
                }

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                var lstViewResult = ParseView(lstModel, _context, _transaction);

                result.Items = lstViewResult;
                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }



        /// <summary>
        /// Gets the model list.
        /// </summary>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<List<TView>>> GetModelListAsync(TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            List<TView> result = new List<TView>();
            try
            {
                var lstModel = await context.Set<TModel>().ToListAsync();

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                result = ParseView(lstModel, _context, _transaction);
                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }


        /// <summary>
        /// Gets the model list asynchronous.
        /// </summary>
        /// <param name="orderBy">The order by.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<PaginationModel<TView>>> GetModelListAsync(
            string orderByPropertyName, OrderByDirection direction, int? pageSize, int? pageIndex
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                dynamic orderBy = GetLambda(orderByPropertyName);
                IQueryable<TModel> sorted = null;
                List<TModel> lstModel = new List<TModel>();
                var query = context.Set<TModel>();

                PaginationModel<TView> result = new PaginationModel<TView>()
                {
                    TotalItems = query.Count(),
                    PageIndex = pageIndex ?? 0
                };
                result.PageSize = pageSize ?? result.TotalItems;

                if (pageSize.HasValue)
                {
                    result.TotalPage = result.TotalItems / pageSize.Value + (result.TotalItems % pageSize.Value > 0 ? 1 : 0);
                }

                switch (direction)
                {
                    case OrderByDirection.Descending:
                        sorted = Queryable.OrderByDescending(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = await sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToListAsync();
                        }
                        else
                        {
                            lstModel = await sorted.ToListAsync();
                        }
                        break;

                    default:
                        sorted = Queryable.OrderBy(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = await sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value).ToListAsync();
                        }
                        else
                        {
                            lstModel = await sorted.ToListAsync();
                        }
                        break;
                }

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);

                var lstViewResult = ParseView(lstModel, _context, _transaction);

                result.Items = lstViewResult;
                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        
        #endregion GetModelList

        #region GetModelListBy

        /// <summary>
        /// Gets the model list by.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual RepositoryResponse<List<TView>> GetModelListBy(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                var lstModel = context.Set<TModel>().Where(predicate).ToList();
                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                var lstViewResult = ParseView(lstModel, _context, _transaction);
                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = true,
                    Data = lstViewResult
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the model list by.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="orderBy">The order by.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual RepositoryResponse<PaginationModel<TView>> GetModelListBy(
            Expression<Func<TModel, bool>> predicate, string orderByPropertyName, OrderByDirection direction, int? pageSize, int? pageIndex
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                dynamic orderBy = GetLambda(orderByPropertyName);
                List<TModel> lstModel = new List<TModel>();
                var query = context.Set<TModel>().Where(predicate);
                IQueryable<TModel> sorted = null;
                PaginationModel<TView> result = new PaginationModel<TView>()
                {
                    TotalItems = query.Count(),
                    PageIndex = pageIndex ?? 0
                };
                result.PageSize = pageSize ?? result.TotalItems;

                if (pageSize.HasValue)
                {
                    result.TotalPage = result.TotalItems / pageSize.Value + (result.TotalItems % pageSize.Value > 0 ? 1 : 0);
                }

                switch (direction)
                {
                    case OrderByDirection.Descending:
                        sorted = Queryable.OrderByDescending(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            

                            lstModel = sorted.Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToList();
                        }
                        else
                        {
                            lstModel = sorted.ToList();
                        }
                        break;

                    default:
                        sorted = Queryable.OrderBy(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToList();
                        }
                        else
                        {
                            lstModel = sorted.ToList();
                        }
                        break;
                }

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                var lstViewResult = ParseView(lstModel, _context, _transaction);
                result.Items = lstViewResult;
                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            // TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }


        /// <summary>
        /// Gets the model list by asynchronous.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<List<TView>>> GetModelListByAsync(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                var query = context.Set<TModel>().Where(predicate);
                var lstModel = await query.ToListAsync();
                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                var result = ParseView(lstModel, _context, _transaction);
                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<List<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the model list by asynchronous.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="orderBy">The order by.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="pageIndex">Index of the page.</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<PaginationModel<TView>>> GetModelListByAsync(
            Expression<Func<TModel, bool>> predicate, string orderByPropertyName
            , OrderByDirection direction, int? pageSize, int? pageIndex
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                dynamic orderBy = GetLambda(orderByPropertyName);
                IQueryable<TModel> sorted = null;
                List<TModel> lstModel = new List<TModel>();
                var query = context.Set<TModel>().Where(predicate);
                
                PaginationModel<TView> result = new PaginationModel<TView>()
                {
                    TotalItems = query.Count(),
                    PageIndex = pageIndex ?? 0
                };
                result.PageSize = pageSize ?? result.TotalItems;

                if (pageSize.HasValue)
                {
                    result.TotalPage = result.TotalItems / pageSize.Value + (result.TotalItems % pageSize.Value > 0 ? 1 : 0);
                }
                switch (direction)
                {
                    case OrderByDirection.Descending:
                        sorted = Queryable.OrderByDescending(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = await sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToListAsync();
                        }
                        else
                        {
                            lstModel = await sorted.ToListAsync();
                        }
                        break;

                    default:
                        sorted = Queryable.OrderBy(query, orderBy);
                        if (pageSize.HasValue)
                        {
                            lstModel = await sorted
                                .Skip(pageIndex.Value * pageSize.Value)
                                .Take(pageSize.Value)
                                .ToListAsync();
                        }
                        else
                        {
                            lstModel = await sorted.ToListAsync();
                        }
                        break;
                }

                lstModel.ForEach(model => context.Entry(model).State = EntityState.Detached);
                var lstViewResult = ParseView(lstModel, _context, _transaction);

                result.Items = lstViewResult;
                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = true,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<PaginationModel<TView>>()
                {
                    IsSucceed = false,
                    Data = null,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

       
        #endregion GetModelListBy

        /// <summary>
        /// Gets the single model.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual RepositoryResponse<TView> GetSingleModel(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                TModel model = context.Set<TModel>().FirstOrDefault(predicate);
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Detached;
                    var viewResult = ParseView(model, context, transaction);
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = viewResult
                    };
                }
                else
                {
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = default(TView)
                    };
                }
            }// TODO: Add more specific exeption types instead of Exception only
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<TView>()
                {
                    IsSucceed = true,
                    Data = default(TView)
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Gets the single model asynchronous.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="isGetSubModels">if set to <c>true</c> [is get sub Items].</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<TView>> GetSingleModelAsync(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            var context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();

            try
            {
                TModel model = await context.Set<TModel>().FirstOrDefaultAsync(predicate);
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Detached;

                    var viewResult = ParseView(model, context, transaction);
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = true,
                        Data = viewResult
                    };
                }
                else
                {
                    return new RepositoryResponse<TView>()
                    {
                        IsSucceed = false,
                        Data = default(TView)
                    };
                }
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<TView>()
                {
                    IsSucceed = true,
                    Data = default(TView)
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the list model.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<bool> RemoveListModel(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                var Items = context.Set<TModel>().Where(predicate).ToList();
                bool result = true;
                if (Items != null)
                {
                    foreach (var model in Items)
                    {
                        if (result)
                        {
                            var r = RemoveModel(model, context, transaction);
                            result = result && r.IsSucceed;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (result)
                    {
                        if (_transaction == null)
                        {
                            //if current transaction is root transaction
                            transaction.Commit();
                        }
                        return new RepositoryResponse<bool>()
                        {
                            IsSucceed = true,
                            Data = true
                        };
                    }
                    else
                    {
                        if (_transaction == null)
                        {
                            //if current transaction is root transaction
                            transaction.Rollback();
                        }
                        return new RepositoryResponse<bool>()
                        {
                            IsSucceed = false,
                            Data = false
                        };
                    }
                }
                else
                {
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the list model asynchronous.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<bool>> RemoveListModelAsync(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                var Items = await context.Set<TModel>().Where(predicate).ToListAsync();
                bool result = true;
                if (Items != null)
                {
                    foreach (var model in Items)
                    {
                        if (result)
                        {
                            var r = await RemoveModelAsync(model, context, transaction);
                            result = result && r.IsSucceed;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (result)
                    {
                        if (_transaction == null)
                        {
                            //if current transaction is root transaction
                            transaction.Commit();
                        }
                        return new RepositoryResponse<bool>()
                        {
                            IsSucceed = true,
                            Data = true
                        };
                    }
                    else
                    {
                        if (_transaction == null)
                        {
                            //if current transaction is root transaction
                            transaction.Rollback();
                        }
                        return new RepositoryResponse<bool>()
                        {
                            IsSucceed = false,
                            Data = false
                        };
                    }
                }
                else
                {
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the model.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<bool> RemoveModel(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)

        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                TModel model = context.Set<TModel>().FirstOrDefault(predicate);
                bool result = true;
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Deleted;
                    result = context.SaveChanges() > 0;
                }

                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = false,
                        Data = false
                    };
                }

            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<bool> RemoveModel(TModel model
            , TDbContext _context = null, IDbContextTransaction _transaction = null)

        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                bool result = true;
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Deleted;
                    result = context.SaveChanges() > 0;
                }

                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = false,
                        Data = false
                    };
                }

            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the model asynchronous.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<bool>> RemoveModelAsync(Expression<Func<TModel, bool>> predicate
            , TDbContext _context = null, IDbContextTransaction _transaction = null)

        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                TModel model = await context.Set<TModel>().FirstOrDefaultAsync(predicate);
                bool result = true;
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Deleted;
                    result = await context.SaveChangesAsync() > 0;
                }

                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = false,
                        Data = false
                    };
                }

            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        // TODO: Should return return enum status code instead
        /// <summary>
        /// Removes the model asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual async Task<RepositoryResponse<bool>> RemoveModelAsync(TModel model
            , TDbContext _context = null, IDbContextTransaction _transaction = null)

        {
            TDbContext context = _context ?? InitContext();
            var transaction = _transaction ?? context.Database.BeginTransaction();
            try
            {
                bool result = true;
                if (model != null)
                {
                    context.Entry(model).State = EntityState.Deleted;
                    result = await context.SaveChangesAsync() > 0;
                }

                if (result)
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Commit();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = true,
                        Data = true
                    };
                }
                else
                {
                    if (_transaction == null)
                    {
                        //if current transaction is root transaction
                        transaction.Rollback();
                    }
                    return new RepositoryResponse<bool>()
                    {
                        IsSucceed = false,
                        Data = false
                    };
                }

            }
            catch (Exception ex)
            {
                LogErrorMessage(ex);
                if (_transaction == null)
                {
                    //if current transaction is root transaction
                    transaction.Rollback();
                }

                return new RepositoryResponse<bool>()
                {
                    IsSucceed = false,
                    Data = false,
                    Ex = ex
                };
            }
            finally
            {
                if (_context == null)
                {
                    //if current Context is Root
                    context.Dispose();
                }
            }
        }

        /// <summary>
        /// Saves the model.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual RepositoryResponse<TView> SaveModel(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            if (CheckIsExists(view.Model, _context, _transaction))
            {
                return EditModel(view, isSaveSubModels, _context, _transaction);
            }
            else
            {
                return CreateModel(view, isSaveSubModels, _context, _transaction);
            }
        }

        /// <summary>
        /// Saves the model asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual Task<RepositoryResponse<TView>> SaveModelAsync(TView view, bool isSaveSubModels = false
            , TDbContext _context = null, IDbContextTransaction _transaction = null)
        {
            if (CheckIsExists(view.Model, _context, _transaction))
            {
                return EditModelAsync(view, isSaveSubModels, _context, _transaction);
            }
            else
            {
                return CreateModelAsync(view, isSaveSubModels, _context, _transaction);
            }
        }

        public virtual bool SaveSubModel(TModel model, TDbContext context, IDbContextTransaction _transaction)
        {
            return false;
        }

        /// <summary>
        /// Saves the model asynchronous.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public virtual Task<bool> SaveSubModelAsync(TModel model, TDbContext context, IDbContextTransaction _transaction)
        {
            throw new NotImplementedException();
        }


        protected LambdaExpression GetLambda(string propName)
        {
            var parameter = Expression.Parameter(typeof(TModel));
            var memberExpression = Expression.Property(parameter, propName);
            return Expression.Lambda(memberExpression, parameter);
        }

        /// <summary>
        /// Logs the error message.
        /// </summary>
        /// <param name="ex">The ex.</param>
        public virtual void LogErrorMessage(Exception ex)
        {
        }
    }
}
