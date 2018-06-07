﻿using System.Collections;
using System.Collections.Generic;
using NSubstitute;
using Tests.TestData.Setup;
using TraktApiSharp.Authentication;
using TraktApiSharp.Objects.Get.Shows;
using TraktApiSharp.Objects.Get.Shows.Episodes;
using TraktApiSharp.Objects.Post.Scrobbles.Responses;
using TraktPluginMP2.Services;
using TraktPluginMP2.Settings;

namespace Tests.TestData.Handler
{
  public class StartScrobbleSeriesTestData : IEnumerable<object[]>
  {
    public IEnumerator<object[]> GetEnumerator()
    {
      yield return new object[]
      {
        new TraktPluginSettings
        {
          EnableScrobble = true,
          UserAuthorized = true
        },
        new MockedDatabaseEpisode("289590", 2, new List<int> {6}, 1).Episode,
        GetMockedTraktClientWithValidAuthorization(),
        "Title_1"
      };
    }

    private ITraktClient GetMockedTraktClientWithValidAuthorization()
    {
      ITraktClient traktClient = Substitute.For<ITraktClient>();
      traktClient.TraktAuthorization.Returns(new TraktAuthorization
      {
        RefreshToken = "ValidToken",
        AccessToken = "ValidToken"
      });

      traktClient.RefreshAuthorization(Arg.Any<string>()).Returns(new TraktAuthorization
      {
        RefreshToken = "ValidToken"
      });
      traktClient.StartScrobbleEpisode(Arg.Any<TraktEpisode>(), Arg.Any<TraktShow>(), Arg.Any<float>()).Returns(
        new TraktEpisodeScrobblePostResponse
        {
          Episode = new TraktEpisode
          {
            Ids = new TraktEpisodeIds { Imdb = "tt12345", Tvdb = 289590 },
            Number = 2,
            Title = "Title_1",
            SeasonNumber = 2
          }
        });

      return traktClient;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}