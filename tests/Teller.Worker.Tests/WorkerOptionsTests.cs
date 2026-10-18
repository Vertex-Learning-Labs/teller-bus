using Teller.Worker;

namespace Teller.Worker.Tests;

public class WorkerOptionsTests
{
    [Theory]
    [InlineData("All", true, true)]
    [InlineData("reads", true, false)]
    [InlineData("POSTS", false, true)]
    public void Lanes_select_which_queues_this_instance_consumes(string lanes, bool reads, bool posts)
    {
        var options = new WorkerOptions { Lanes = lanes };

        options.EnsureValid();

        Assert.Equal(reads, options.RunsReads);
        Assert.Equal(posts, options.RunsPosts);
    }

    [Fact]
    public void An_unknown_lane_fails_startup()
    {
        var options = new WorkerOptions { Lanes = "transfers" };

        Assert.Throws<InvalidOperationException>(options.EnsureValid);
    }
}
