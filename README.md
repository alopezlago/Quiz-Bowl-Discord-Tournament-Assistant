To add the bot to the server:
https://discordapp.com/oauth2/authorize?client_id=505827616469680129&scope=bot

Temporary invite: https://discord.gg/6tQvfT

Need to add try/catch handlers so we don't blow up on exceptions

The bot needs to have all of these permissions (give it a role), because it needs to grant readers these permissions:

UseVoiceDetection
UseVoice
Speak
SendMessages
KickMembers
MuteMembers
DeafenMembers
AddReactions
PrioritySpeaker (256)


Starting a tournament:
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


TODO: Do some refactoring of tournament state to separate individual fields into more explainable components. Specifically:
  - Make the state thread-safe, which requires using concurrent collections and locking.
  - Consider splitting the state between Discord-specific state and tournament-specific state.
      - Once this is decided, then hide the Discord-specific collections in the state behind methods.
TODO: Add command to switch readers (may not be needed since you can just assign the role to the user)
TODO: Add command to list players and the teams they are on (can be DM to the TD)
TODO: Track member name changes so we can update reader/player names.
TODO: Be smarter about which channels we delete
TODO: Add multi-guild support
TODO: Write guide for how to start a tournament as a TD
TODO: Add persistence of tournaments
TODO: Have an option to allow for more automation, that would enable:
  - !win <team> in the channel would track who won that game, and would grant them permissions for the next room.
  - Tracking player buzzes. Either use a queue and clear on a score (0/10/15, etc.), or store a timestamp of the last buzz in the channel and mention the user if it's been long enough.
  - Longer term, potentially look at merging with the tournament runner and tracking all of the events.
  