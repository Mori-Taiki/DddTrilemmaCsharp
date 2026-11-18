import json, urllib.request, urllib.error
import os
base=os.environ.get('API_BASE_URL', 'http://localhost:5080')
def call(path, status, data=None):
    req=urllib.request.Request(base+path, data=json.dumps(data).encode() if data else None, headers={'Content-Type':'application/json'},method='PUT' if data else 'GET')
    try: response=urllib.request.urlopen(req)
    except urllib.error.HTTPError as e: response=e
    body=response.read()
    assert response.status==status,(path,response.status,body)
    return json.loads(body) if body else None
for viewer,code,count in [('author',200,0),('guest',403,1),('follower',403,3),('vip',200,2),('buyer',200,3)]:
    r=call('/stories/followers-paid/view?viewer='+viewer,code)
    assert r['factQueries']==count,r
    assert r['trace'][-1] == ('許可' if code == 200 else '拒否'),r
    if viewer == 'guest': assert r['reason'] == 'フォローが必要',r
    if viewer == 'follower': assert r['reason'] == '購入が必要',r
call('/stories/missing/view?viewer=guest',404)
call('/stories/public-free/view',400)
call('/stories/public-free/visibility',403,{'actor':'guest','visibility':'フォロワー限定'})
call('/stories/public-free/visibility',400,{'actor':'author','visibility':99})
call('/stories/public-free/visibility',400,{'actor':'author'})
call('/stories/public-free/visibility',200,{'actor':'author','visibility':'フォロワー限定'})
call('/stories/public-free/view?viewer=guest',403)
call('/stories/public-free/visibility',409,{'actor':'author','visibility':'フォロワー限定'})
call('/stories/public-free/visibility',200,{'actor':'author','visibility':'全体公開'})
call('/stories/public-free/view?viewer=guest',200)
print('PASS: HTTP scenarios, validation, denial, update, read-after-write; seed state restored')
