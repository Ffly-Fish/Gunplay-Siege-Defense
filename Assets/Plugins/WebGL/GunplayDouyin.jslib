// GunplayDouyin.jslib —— 抖音小游戏（tt）桥接：SDK 检测、激励视频、分享、启动参数
// Unity WebGL 通过 DllImport("__Internal") 调用这里的函数；字符串参数用 UTF8ToString 读取。
mergeInto(LibraryManager.library, {
  // 是否处于抖音小游戏环境（存在 tt 且 getSystemInfoSync 可用）：1 = 是，0 = 否
  GunplayDouyin_HasSdk: function () {
    try {
      return (typeof tt !== 'undefined' && tt && typeof tt.getSystemInfoSync === 'function') ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  // 播放激励视频：结束后用 SendMessage(objectName, callbackMethod, "1"/"0") 回传是否看完
  GunplayDouyin_ShowRewardedVideo: function (placementIdPtr, objectNamePtr, callbackMethodPtr) {
    var placementId = UTF8ToString(placementIdPtr);
    var objectName = UTF8ToString(objectNamePtr);
    var callbackMethod = UTF8ToString(callbackMethodPtr);
    var finished = false;

    function finish(watched) {
      if (finished) {
        return;
      }
      finished = true;
      try {
        SendMessage(objectName, callbackMethod, watched ? "1" : "0");
      } catch (e) {
      }
    }

    try {
      if (typeof tt === 'undefined' || !tt || typeof tt.createRewardedVideoAd !== 'function') {
        finish(false);
        return;
      }

      var ad = tt.createRewardedVideoAd({ adUnitId: placementId });
      ad.onClose(function (result) {
        finish(!!(result && result.isEnded));
      });
      ad.onError(function (error) {
        try {
          console.warn('[GunplayDouyin] rewarded video error: ' + JSON.stringify(error));
        } catch (e) {
        }
        finish(false);
      });

      if (typeof ad.load === 'function') {
        ad.load()
          .then(function () {
            return ad.show();
          })
          .catch(function (error) {
            try {
              console.warn('[GunplayDouyin] rewarded video load/show failed: ' + JSON.stringify(error));
            } catch (e) {
            }
            finish(false);
          });
      } else {
        var shown = ad.show();
        if (shown && typeof shown.catch === 'function') {
          shown.catch(function (error) {
            try {
              console.warn('[GunplayDouyin] rewarded video show failed: ' + JSON.stringify(error));
            } catch (e) {
            }
            finish(false);
          });
        }
      }
    } catch (e) {
      try {
        console.warn('[GunplayDouyin] rewarded video exception: ' + e);
      } catch (inner) {
      }
      finish(false);
    }
  },

  // 分享：返回 1 = 已发起分享，0 = 未接入或调用失败
  GunplayDouyin_Share: function (titlePtr) {
    try {
      if (typeof tt === 'undefined' || !tt || typeof tt.shareAppMessage !== 'function') {
        return 0;
      }
      tt.shareAppMessage({ title: UTF8ToString(titlePtr) });
      return 1;
    } catch (e) {
      return 0;
    }
  },

  // 启动参数：返回 tt.getLaunchOptionsSync().query 的 JSON（malloc 分配，调用方用 GunplayDouyin_Free 释放）
  GunplayDouyin_GetLaunchQuery: function () {
    try {
      if (typeof tt === 'undefined' || !tt || typeof tt.getLaunchOptionsSync !== 'function') {
        return 0;
      }
      var options = tt.getLaunchOptionsSync() || {};
      var query = options.query || {};
      var json = JSON.stringify(query);
      var length = lengthBytesUTF8(json) + 1;
      var buffer = _malloc(length);
      stringToUTF8(json, buffer, length);
      return buffer;
    } catch (e) {
      return 0;
    }
  },

  // 释放 GunplayDouyin_GetLaunchQuery 返回的字符串内存
  GunplayDouyin_Free: function (pointer) {
    if (pointer) {
      _free(pointer);
    }
  }
});
