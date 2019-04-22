using System;
using System.Threading.Tasks;
using System.Linq.Expressions;
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
        /// The name of the repository's entity type.
        /// </summary>
        protected readonly string typeName;

        /// <summary>
        /// Creates a new MongoDB repository.
        /// </summary>
        /// <param name="db">The Mongo database of which you want to create a repository.</param>
        protected MongoDBRepository(IMongoDatabase db)
        {
            this.db = db;
            this.typeName = typeof(T).Name;

            collection = db.GetCollection<T>(typeName);
            if (collection is null)
            {
                throw new MongoException($"{nameof(MongoDBRepository<T>)}::ctor: No collection named \"{typeName}\" found in database!");
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
            IAsyncCursor<T> cursor = await collection.FindAsync(u => u.Id == id);
            return await cursor.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets all entities from the repository.
        /// </summary>
        /// <returns>All entities inside the repo.</returns>
        public Task<IEnumerable<T>> GetAll()
        {
            IEnumerable<T> r = collection.AsQueryable().ToEnumerable();
            return Task.FromResult(r);
        }

        /// <summary>
        /// Finds all entities according to the specified predicate <see cref="T:System.Linq.Expressions.Expression" />.
        /// </summary>
        /// <param name="predicate">The search predicate (all entities that match the provided conditions will be added to the query's result).</param>
        /// <returns>The found entities (<see cref="T:System.Collections.Generic.IEnumerable`1" />).</returns>
        public async Task<IEnumerable<T>> Find(Expression<Func<T, bool>> predicate)
        {
            return (await collection.FindAsync(predicate)).ToEnumerable();
        }

        /// <summary>
        /// Gets a single entity from the repo according to the specified predicate condition.<para></para>
        /// If 0 or &gt;1 entities are found, <c>null</c> is returned.
        /// </summary>
        /// <param name="predicate">The search predicate.</param>
        /// <returns>Single found entity; <c>null</c> if 0 or &gt;1 entities were found.</returns>
        public async Task<T> SingleOrDefault(Expression<Func<T, bool>> predicate)
        {
            IAsyncCursor<T> cursor = await collection.FindAsync(predicate);
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
                await collection.InsertOneAsync(entity);
                return true;
            }
            catch (Exception)
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
                await collection.InsertManyAsync(entities);
                return true;
            }
            catch (Exception)
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
            var r = await collection.DeleteOneAsync(u => u.Id == entity.Id);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes the specified entity.
        /// </summary>
        /// <param name="id">The unique id of the entity to remove.</param>
        /// <returns>Whether the entity could be removed successfully or not.</returns>
        public async Task<bool> Remove(ObjectId id)
        {
            var r = await collection.DeleteOneAsync(u => u.Id == id);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes all entities at once from the repository.
        /// </summary>
        /// <returns>Whether the entities were removed successfully or not. If the repository was already empty, <c>false</c> is returned (because nothing was actually &lt;&lt;removed&gt;&gt; ).</returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<bool> RemoveAll()
        {
            var r = await collection.DeleteManyAsync(FilterDefinition<T>.Empty);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes all entities that match the specified conditions (via the predicate <see cref="T:System.Linq.Expressions.Expression" /> parameter).
        /// </summary>
        /// <param name="predicate">The predicate <see cref="T:System.Linq.Expressions.Expression" /> that defines which entities should be removed.</param>
        /// <returns>Whether the entities were removed successfully or not.</returns>
        public async Task<bool> RemoveRange(Expression<Func<T, bool>> predicate)
        {
            var r = await collection.DeleteManyAsync(predicate);
            return r.IsAcknowledged;
        }

        /// <summary>
        /// Removes the range of entities from the repository.
        /// </summary>
        /// <param name="entities">The entities to remove.</param>
        /// <returns>Whether the entities were removed successfully or not.</returns>
        public async Task<bool> RemoveRange(IEnumerable<T> entities)
        {
            if (entities is null)
            {
                return false;
            }

            bool success = true;
            var tasks = new List<Task>(8);
            foreach (T entity in entities)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (!collection.DeleteOne(u => u.Id == entity.Id).IsAcknowledged)
                    {
                        success = false;
                    }
                }));
            }
            await Task.WhenAll(tasks);
            return success;
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

            bool success = true;
            var tasks = new List<Task>(8);
            foreach (ObjectId id in ids)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (!collection.DeleteOne(u => u.Id == id).IsAcknowledged)
                    {
                        success = false;
                    }
                }));
            }
            await Task.WhenAll(tasks);
            return success;
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
