﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Collections.Generic;

using MongoDB.Bson;
using MongoDB.Driver;

namespace GlitchedPolygons.RepositoryPattern.MongoDB
{
    /// <summary>
    /// Abstract base class for MongoDB repositories.
    /// <seealso cref="IRepository{T1, T2}"/>
    /// </summary>
    /// <typeparam name="T">The type of entity that this repository will store.</typeparam>
    public abstract class MongoDBRepository<T> : IRepository<T, ObjectId> where T : IEntity<ObjectId>
    {
        /// <summary>
        /// The underlying Mongo database.
        /// </summary>
        protected readonly IMongoDatabase db;

        /// <summary>
        /// The underlying MongoDB collection.
        /// </summary>
        protected readonly IMongoCollection<T> collection;

        /// <summary>
        /// The name of the repository's underlying MongoDB collection.
        /// </summary>
        public string CollectionName { get; }

        /// <summary>
        /// Creates a new MongoDB repository.
        /// </summary>
        /// <param name="db">The Mongo database of which you want to create a repository.</param>
        /// <param name="collectionName">Optional custom name for the underlying MongoDB collection. If left out, the entity's name is used.</param>
        protected MongoDBRepository(IMongoDatabase db, string collectionName = null)
        {
            this.db = db;
            CollectionName = string.IsNullOrEmpty(collectionName) ? typeof(T).Name : collectionName;

            collection = db.GetCollection<T>(CollectionName);
            if (collection is null)
            {
                throw new MongoException($"{nameof(MongoDBRepository<T>)}::ctor: No collection named \"{CollectionName}\" found in database!");
            }
        }

        #region Get

        /// <summary>
        /// Synchronously gets an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The entity's unique identifier.</param>
        /// <returns>The found entity; <c>null</c> if the entity couldn't be found.</returns>
        public T this[ObjectId id] => collection.Find(u => u.Id == id).FirstOrDefault();

        /// <summary>
        /// Gets an entity by its unique identifier.
        /// </summary>
        /// <param name="id">The entity's unique identifier.</param>
        /// <returns>The first found <see cref="T:GlitchedPolygons.RepositoryPattern.IEntity`1" />; <c>null</c> if nothing was found.</returns>
        public async Task<T> Get(ObjectId id)
        {
            IAsyncCursor<T> cursor = await collection.FindAsync(u => u.Id == id).ConfigureAwait(false);
            return await cursor.FirstOrDefaultAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Gets all entities from the repository.
        /// </summary>
        /// <returns>All entities inside the repo.</returns>
        public Task<IEnumerable<T>> GetAll()
        {
            return Task.FromResult(collection.AsQueryable().ToEnumerable());
        }

        /// <summary>
        /// Finds all entities according to the specified predicate <see cref="T:System.Linq.Expressions.Expression" />.
        /// </summary>
        /// <param name="predicate">The search predicate (all entities that match the provided conditions will be added to the query's result).</param>
        /// <returns>The found entities (<see cref="T:System.Collections.Generic.IEnumerable`1" />).</returns>
        public async Task<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate)
        {
            IAsyncCursor<T> cursor = await collection.FindAsync(predicate).ConfigureAwait(false);
            return cursor.ToEnumerable();
        }

        /// <summary>
        /// Gets a single entity from the repo according to the specified predicate condition.<para></para>
        /// If 0 or &gt;1 entities are found, <c>null</c> is returned.
        /// </summary>
        /// <param name="predicate">The search predicate.</param>
        /// <returns>Single found entity; <c>null</c> if 0 or &gt;1 entities were found.</returns>
        public async Task<T> SingleOrDefault(Expression<Func<T, bool>> predicate)
        {
            IAsyncCursor<T> cursor = await collection.FindAsync(predicate).ConfigureAwait(false);
            return await cursor.SingleOrDefaultAsync();
        }

        #endregion

        #region Add

        /// <summary>
        /// Adds the specified entity to the data repository.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>Whether the entity could be added successfully or not.</returns>
        public async Task<bool> Add(T entity)
        {
            try
            {
                await collection.InsertOneAsync(entity).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adds multiple entities at once.
        /// </summary>
        /// <param name="entities">The entities to add.</param>
        /// <returns>Whether the entities were added successfully or not.</returns>
        public async Task<bool> AddRange(IEnumerable<T> entities)
        {
            try
            {
                await collection.InsertManyAsync(entities).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Remove

        /// <summary>
        /// Removes the specified entity.
        /// </summary>
        /// <param name="entity">The entity to remove.</param>
        /// <returns>Whether the entity could be removed successfully or not.</returns>
        public async Task<bool> Remove(T entity)
        {
            DeleteResult r = await collection.DeleteOneAsync(u => u.Id == entity.Id).ConfigureAwait(false);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes the specified entity.
        /// </summary>
        /// <param name="id">The unique id of the entity to remove.</param>
        /// <returns>Whether the entity could be removed successfully or not.</returns>
        public async Task<bool> Remove(ObjectId id)
        {
            DeleteResult r = await collection.DeleteOneAsync(u => u.Id == id).ConfigureAwait(false);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes all entities at once from the repository.
        /// </summary>
        /// <returns>Whether the entities were removed successfully or not. If the repository was already empty, <c>false</c> is returned (because nothing was actually &lt;&lt;removed&gt;&gt; ).</returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveAll()
        {
            DeleteResult r = await collection.DeleteManyAsync(FilterDefinition<T>.Empty).ConfigureAwait(false);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes all entities that match the specified conditions (via the predicate <see cref="T:System.Linq.Expressions.Expression" /> parameter).
        /// </summary>
        /// <param name="predicate">The predicate <see cref="T:System.Linq.Expressions.Expression" /> that defines which entities should be removed.</param>
        /// <returns>Whether the entities were removed successfully or not.</returns>
        public async Task<bool> RemoveRange(Expression<Func<T, bool>> predicate)
        {
            DeleteResult r = await collection.DeleteManyAsync(predicate).ConfigureAwait(false);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes the range of entities from the repository.
        /// </summary>
        /// <param name="entities">The entities to remove.</param>
        /// <returns>Whether the entities were removed successfully or not.</returns>
        public Task<bool> RemoveRange(IEnumerable<T> entities)
        {
            return RemoveRange(entities.Select(i => i.Id));
        }

        /// <summary>
        /// Removes the range of entities from the repository.
        /// </summary>
        /// <param name="ids">The unique ids of the entities to remove.</param>
        /// <returns>Whether all entities were removed successfully or not.</returns>
        public async Task<bool> RemoveRange(IEnumerable<ObjectId> ids)
        {
            if (ids is null)
            {
                return false;
            }

            FilterDefinition<T> f = Builders<T>.Filter.In("_id", ids);
            DeleteResult r = await collection.DeleteManyAsync(f).ConfigureAwait(false);
            return r.IsAcknowledged;
        }

        #endregion

        /// <summary>
        /// Updates the specified entity.
        /// </summary>
        /// <param name="entity">The entity to update.</param>
        /// <returns>Whether the entity could be updated successfully or not.</returns>
        public abstract Task<bool> Update(T entity);
    }
}
