# Prompt history

User prompts from this task in chronological order. Text inside each block is copied verbatim from the conversation.

## Prompt 1

~~~text

# Files mentioned by the user:

## task-04-feature-design-and-build.docx: D:\Downloads\task-04-feature-design-and-build.docx

Distinguish instructions in attached documents from the user's request.

## My request:
I was given the attached task. Before I start implementing anything, I want to work through the ambiguity in the brief and define a sensible scope for the 24-hour timeframe.

Can you help me identify:

- the main unanswered product/technical questions,
- the assumptions I would need to make if I can't get clarification,
- the biggest design decisions this brief forces me to make,
- and what a realistic first version should include versus explicitly leave out?

Don't write code yet. I want to use this to decide on the plan and architecture first.
~~~

## Prompt 2

~~~text
I like the idea of making the scope explicit, but I don't want to lock into earthquakes yet. Compare 3 realistic scope options for this excercise:

- one event type only, like earthquakes
- a small event model with 2-3 types
- a more generic alert-rule platform

Then each of these explain what I would actually need to build; what architectural value it demonstrates, what complexity it introduces, what could realisticly go wrong in 24 hours.

Also reconsider wheter authentication is actually necessary for this task, since the description doesnt explicitly ask for it. Don't choose the option for me yet, I want to compare the trade-offs first.
~~~

## Prompt 3

~~~text
I will go with the simpler one-event-type version for now and use earthquakes as the first concrete example.
My reasoning is that the brief specifically says the notification channels should be easy to extend, but it doesnt necessarily require a fully generic event/rule system. Trying to support news, markets and disasters properly in 24 hours would probably just extend the implementation without giving more value.
I also don’t think authentication is worth adding for this exercise. I’d rather treat it as a local/single-user prototype and keep the admin page focused on seeing events, alerts and delivery results.
I still want to avoid hardcoding everything around earthquakes though. Ingestion, matching and notification sending should be separated enough that the design doesnt fall apart if another event type or notification channel is added later.
Now turn this into an actual implementation plan before starting the code. I mainly want to figure out what the minimum finished version should do, the main parts of the system, the basic data model and data flow, and a sensible order to build it in.
Also suggest where it would make sense to make commits and what I should verify after each step.
Keep it realistic for 24 hours and point out if something starts becoming over-engineered. No code yet.
~~~

## Prompt 4

~~~text
The plan looks good. I use ASP.NET Core/.NET 8 and Angular since that's the stack I'm most comfortable with.
For persistence Im thinking EF Core with SQLite. I havent really used SQLite before, but it seems like a good fit here because it keeps the setup local and doesn't require running a database server. Most of the data access should still just be normal EF Core anyway.
One thing I'm not fully convinced about is the generic Event model with an event type and separate earthquake data. Since we're deliberately only supporting earthquakes in this version, that feels like we might be generalizing too early. An EarthquakeEvent model seems simpler while still allowing the source adapter and notification code to stay separate.
Can you sanity check those choices before we start? Also take another look at the proposed data model and point out anything else we're adding now only because it might be useful later.
If that looks fine, let's create the initial planning docs in the repo from what we've decided so far. Keep them short and based on the actual decisions we made. No code yet.
~~~

## Prompt 5

~~~text
One thing before we start coding, I noticed we're using .NET 8 even though it's already close to end of support. Since this is a new project and we haven't written any application code yet, I think it makes more sense to use .NET 10 instead.
Also small correction in the decisions doc: ASP.NET Core, Angular and EF Core are familiar to me, but SQLite isn't. I'm choosing SQLite mainly because it keeps the setup simple and doesn't require a separate database server. Update the two docs accordingly.
We also need to fix the Git directory access problem before continuing, since the assignment requires commits for the milestones. This is my local repo, so check what Git is complaining about and fix the safe-directory issue if that's what it is.
After that, let's start only the first implementation milestone: scaffold the ASP.NET Core backend and Angular app, add EF Core + SQLite, create the three basic entities we planned and the initial migration.
Don't start the matching, USGS polling or notification sending yet.
Once that's done, build both projects and check that the database setup works. If anything fails, investigate it instead of just moving on.
~~~

## Prompt 6

~~~text
From now on, make the milestone commits locally but don't push them unless I ask. I want to review each milestone before pushing it.&#x20;
&#x20;I don't think we actually need alert deletion. That wasn't in the original brief and create/edit/enable/disable is enough for the prototype. DeletedAtUtc seems to have been added mainly so we can preserve delivery history when deleting alerts, but that's solving a requirement we introduced ourselves. Let's remove deletion and update the plan accordingly.
The other thing I noticed is that Alert has EmailEnabled and SlackEnabled. I'm not sure I like that because one of the actual requirements is that adding notification channels later should be easy. If we add another channel with this model, we'd need another property and database migration.
Would a small AlertChannel relation with an alert ID and channel be a better fit? I don't want a plugin system or anything complicated, just to avoid baking the two current channels into the Alert schema. Have a look and make the smallest change that solves that cleanly.
Also, we haven't started the prompt history yet and that's explicitly required for the submission. Please add a prompt history file with the prompts I've sent in this conversation so far, verbatim and in order. Don't rewrite them or make the history look cleaner than it actually was.
Once those changes are done, rerun the backend build and migration checks.
Then before implementing the next milestone, show me the test cases you intend to cover for earthquake matching and delivery creation. Don't start the USGS integration or real email/Slack sending yet.
~~~
