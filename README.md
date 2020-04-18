## Quiz Bowl Discord Tournament Assistant


#### Setup

The bot needs to have all of these permissions (give it a role), because it needs to grant readers these permissions:

- Use Voice Activity
- Manage Roles
- Kick Members
- Read Text Channels & See Voice Channels
- Send Messages
- Read Message History
- Add Reactions
- Connect
- Speak
- Mute Members
- Defean Members
- Move Members
- Priority Speaker

#### Running a tournament

  1. Create your tournament by adding yourself as a TD to a tournament.
  
    !addTD @my_user <My tournament name>

  2. Start the setup phase with !setup

    !setup

  3. Add all of the readers based on their mentions.

    @Reader1  @Reader2

  4. Set the number of round robins to play with

    3

  5. Add all of the teams in the tournament

    Team 1, Team 2, Team 3

  6. Players will join their team by clicking on the reaction that matches the team.

  7. Start the tournament with !start.

    !start

  8. The bot will create the text and voice channels for everyone, and assign permissions for these rooms.
  9. If you need to set up a finals room, use the !finals command.

    !finals @Reader Team1, Team2

  10. To end the tournament, use !end

    !end