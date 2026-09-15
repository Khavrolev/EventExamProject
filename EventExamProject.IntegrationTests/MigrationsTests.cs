using System.Data;
using EventExamProject.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventExamProject.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class MigrationsTests(DatabaseFixture fixture) : RepositoryTestBase(fixture)
{
    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = (NpgsqlConnection)Context.Database.GetDbConnection();

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    [Fact]
    public async Task Migrate_ShouldCreateEventsAndBookingsTables()
    {
        // Arrange
        var connection = await OpenConnectionAsync();

        // Act
        await using var command = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tableNames = new List<string>();
        while (await reader.ReadAsync())
        {
            tableNames.Add(reader.GetString(0));
        }

        // Assert
        tableNames.Should().Contain(["events", "bookings"]);
    }

    [Fact]
    public async Task Migrate_ShouldCreatePrimaryKeys_OnBothTables()
    {
        // Arrange
        var connection = await OpenConnectionAsync();

        // Act
        await using var command = new NpgsqlCommand(
            """
            SELECT tc.table_name
            FROM information_schema.table_constraints tc
            WHERE tc.constraint_type = 'PRIMARY KEY' AND tc.table_name IN ('events', 'bookings')
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var tablesWithPrimaryKey = new List<string>();
        while (await reader.ReadAsync())
        {
            tablesWithPrimaryKey.Add(reader.GetString(0));
        }

        // Assert
        tablesWithPrimaryKey.Should().BeEquivalentTo(["events", "bookings"]);
    }

    [Fact]
    public async Task Migrate_ShouldCreateForeignKey_FromBookingsToEvents_WithCascadeDelete()
    {
        // Arrange
        var connection = await OpenConnectionAsync();

        // Act
        await using var command = new NpgsqlCommand(
            """
            SELECT kcu.column_name, ccu.table_name AS referenced_table, ccu.column_name AS referenced_column, rc.delete_rule
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name
            JOIN information_schema.constraint_column_usage ccu ON tc.constraint_name = ccu.constraint_name
            JOIN information_schema.referential_constraints rc ON tc.constraint_name = rc.constraint_name
            WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_name = 'bookings'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue("bookings should have a foreign key to events");
        var columnName = reader.GetString(0);
        var referencedTable = reader.GetString(1);
        var referencedColumn = reader.GetString(2);
        var deleteRule = reader.GetString(3);

        // Assert
        columnName.Should().Be("EventId");
        referencedTable.Should().Be("events");
        referencedColumn.Should().Be("Id");
        deleteRule.Should().Be("CASCADE");
    }
}
