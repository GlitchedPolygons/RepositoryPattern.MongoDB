using System;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Collections.Generic;

using MongoDB.Bson;
using MongoDB.Driver;
using GlitchedPolygons.RepositoryPattern;

namespace RepositoryPattern.MongoDB
{
    public abstract class MongoDBRepository<TEntity> : IRepository<TEntity, ObjectId> where TEntity : IEntity<ObjectId>
    {
        /// <summary>
        /// The underlying Mongo database.
        /// </summary>
        protected readonly IMongoDatabase db;

        /// <summary>
        /// The underlying MongoDB collection.
        /// </summary>
        protected readonly IMongoCollection<TEntity> collection;

        /// <summary>
        /// Creates a new MongoDB repository.
        /// </summary>
        /// <param name="db">The Mongo database of which you want to create a repository.</param>
        protected MongoDBRepository(IMongoDatabase db)
        {
            this.db = db;
            collection = db.GetCollection<TEntity>(typeof(TEntity).Name);
            if (collection is null)
            {
                throw new MongoException($"{nameof(MongoDBRepository<TEntity>)}::ctor: ");
            }
        }

        public TEntity this[ObjectId id] => collection.Find(u => u.Id == id).FirstOrDefault();

        public async Task<bool> Add(TEntity entity)
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

        public async Task<bool> AddRange(IEnumerable<TEntity> entities)
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

        public async Task<IEnumerable<TEntity>> Find(Expression<Func<TEntity, bool>> predicate)
        {
            return (await collection.FindAsync(predicate)).ToEnumerable();
        }

        public async Task<TEntity> Get(ObjectId id)
        {
            IAsyncCursor<TEntity> cursor = await collection.FindAsync(u => u.Id == id);
            return await cursor.FirstOrDefaultAsync();
        }

        public Task<IEnumerable<TEntity>> GetAll()
        {
            IEnumerable<TEntity> r = collection.AsQueryable().ToEnumerable();
            return Task.FromResult(r);
        }

        public async Task<bool> Remove(TEntity entity)
        {
            var r = await collection.DeleteOneAsync(u => u.Id == entity.Id);
            return r.IsAcknowledged;
        }

        public async Task<bool> Remove(ObjectId id)
        {
            var r = await collection.DeleteOneAsync(u => u.Id == id);
            return r.IsAcknowledged;
        }

        public Task<bool> RemoveAll()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> RemoveRange(Expression<Func<TEntity, bool>> predicate)
        {
            var r = await collection.DeleteManyAsync(predicate);
            return r.IsAcknowledged;
        }

        public Task<bool> RemoveRange(IEnumerable<TEntity> entities)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveRange(IEnumerable<ObjectId> ids)
        {
            throw new NotImplementedException();
        }

        public async Task<TEntity> SingleOrDefault(Expression<Func<TEntity, bool>> predicate)
        {
            IAsyncCursor<TEntity> cursor = await collection.FindAsync(predicate);
            return await cursor.SingleOrDefaultAsync();
        }

        public abstract Task<bool> Update(TEntity entity);
    }
}
