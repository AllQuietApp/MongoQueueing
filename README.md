# MongoQueueing
Message Queueing for .NET Core with MongoDB

![Test Workflow](https://github.com/AllQuietApp/MongoQueueing/actions/workflows/test.yml/badge.svg)

## Usage

TODO

## Running Tests

### Run MongoDB locally
To run the integration tests you need a running mongo instance which you can connect to. If you have docker installed, an easy way to do this is to simply spin up a container:
    
    docker run --name mongo -d --restart unless-stopped -p 27017:27017 mongo:6.0.2

The command above will start a mongo container listening on the default port 27017. Docker will keep the container running and preserves its state during restarts of the docker host (and your computer).

If you already have a running MongoDB that you'd like to use for the integration tests, you can modify the connection string in: `./src/AllQuiet.MongoQueueing.Tests/config.json`

### Run the tests

Run the unit and integration tests in the project's root (where the sln is located):

    dotnet test