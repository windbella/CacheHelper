# CacheHelper
.Net 기반 Api를 위한 캐싱 라이브러리

### 데이터 중복 로드를 막기 위한 기능
(캐시에 데이터가 없을 때 동시에 캐시 관련 블럭에 진입하여 (웹 API와 같은 멀티 쓰레딩 환경)
캐시 데이터를 불러오는 코드가 여러번 호출되는 상황)

각 키 단위로 SemaphoreSlim을 이용하여 쓰레드 상에서 안전하게 데이터를 저장하고 불러오게 됩니다.
( 저장과 불러오기가 묶여 있어 데이터 불러오기 및 저장 중에 캐시를 불러올 때 데이터 저장을 기다렸다 데이터를 가져가게 됩니다. )

```
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
```
### ex)
```
CacheManager myCache = new CacheManager(); // 캐시 생성

myCache.CacheDataValidator = (data) =>
{
    if(data == null) // 캐시에 저장될 데이터가 null인 경우
    {
        return false; // 캐시에 저장하지 않도록 설정
    }
    return true; // 캐시에 저장
};
```

```
CacheDataLoader<DataTable> loader = () =>
{
    DataTable dataTable;
    try
    {
        // dataTable에 DB에서 가져온 데이터를 담음
    }
    catch
    {
        dataTable = null; // 실패 했을 경우 null을 반환
    }
    return dataTable;
};

DataTable cachedData = myCache.Get<DataTable>("데이터 구분 키", loader, new TimeSpan(0, 1, 0));
```
