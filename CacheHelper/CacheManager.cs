using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CacheHelper
{
    public delegate T CacheDataLoader<T>();
    public delegate bool CacheDataValidator<T>(T data);

    public class CacheManager
    {
        /// <summary>
        /// 캐시 객체
        /// </summary>
        public ObjectCache Cache { get; set; }
        /// <summary>
        /// 캐시 불러오기 대기 시간
        /// </summary>
        public TimeSpan LockTimeout { get; set; }
        /// <summary>
        /// 캐시 저장 전 유효성 검사기
        /// </summary>
        public CacheDataValidator<object> CacheDataValidator { get; set; }

        // 세마포어 생성 관리 세마포어
        private SemaphoreSlim mainLocker = new SemaphoreSlim(1);

        public CacheManager()
        {
            Cache = MemoryCache.Default;
            SetDefault();
        }

        public CacheManager(ObjectCache cache)
        {
            Cache = cache;
            SetDefault();
        }

        private void SetDefault()
        {
            LockTimeout = new TimeSpan(0, 5, 0);
            CacheDataValidator = (data) => { return true; };
        }

        /// <summary>
        /// 캐시 데이터 불러오기
        /// </summary>
        /// <typeparam name="T">반환 타입</typeparam>
        /// <param name="key">캐시 키</param>
        /// <returns></returns>
        public T Get<T>(string key) where T : class
        {
            try
            {
                return (T)Cache[key];
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 캐시 데이터 불러오기
        /// 데이터가 없는 경우 Loader 함수를 통하여 데이터 생성 및 저장
        /// </summary>
        /// <typeparam name="T">반환 타입</typeparam>
        /// <param name="key">캐시 키</param>
        /// <param name="loader">데이터를 불러오는 로직</param>
        /// <param name="cacheTime">캐싱 시간</param>
        /// <returns>반환 결과</returns>
        public T Get<T>(string key, CacheDataLoader<T> loader, TimeSpan cacheTime) where T : class
        {
            SemaphoreSlim locker = GetLocker(key, cacheTime);
            T data = null;
            if (locker.Wait(LockTimeout))
            {
                try
                {
                    data = Get<T>(key);
                    if (data == null)
                    {
                        data = loader();
                        if (CacheDataValidator(data))
                        {
                            Set(key, data, cacheTime);
                        }
                    }
                }
                catch { }
                finally
                {
                    locker.Release();
                }
            }
            else
            {
                string lockerKey = GetLockerKey(key);
                if (locker.Equals(Get<SemaphoreSlim>(lockerKey)))
                {
                    Remove(lockerKey);
                }
            }
            return data;
        }

        /// <summary>
        /// 캐시 데이터 삭제하기
        /// </summary>
        /// <param name="key">캐시 키</param>
        public void Remove(string key)
        {
            Cache.Remove(key);
        }

        /// <summary>
        /// 캐시 데이터 전체 삭제하기
        /// </summary>
        public void Clear()
        {
            foreach (string key in Cache.Select(item => item.Key).ToList())
            {
                Remove(key);
            }
        }

        /// <summary>
        /// 캐시 데이터 저장하기
        /// </summary>
        /// <param name="key">캐시 키</param>
        /// <param name="data">저장할 데이터</param>
        /// <param name="cacheTime">캐싱 시간</param>
        public void Set(string key, object data, TimeSpan cacheTime)
        {
            Cache.Add(key, data, DateTime.Now.AddTicks(cacheTime.Ticks));
        }

        // 데이터 중복 생성 방지 세마포어 반환
        private SemaphoreSlim GetLocker(string key, TimeSpan cacheTime)
        {
            SemaphoreSlim locker = null;
            mainLocker.Wait();
            try
            {
                key = GetLockerKey(key);
                locker = Get<SemaphoreSlim>(key);
                if (locker == null)
                {
                    locker = new SemaphoreSlim(1);
                    Set(key, locker, cacheTime);
                }
            }
            catch
            {
                locker = new SemaphoreSlim(1);
            }
            finally
            {
                mainLocker.Release();
            }
            return locker;
        }

        private string GetLockerKey(string key)
        {
            return string.Format("locker@{0}", key);
        }
    }
}
