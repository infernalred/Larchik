UPDATE instruments
SET type = 1
WHERE isin IN (
    'RU000A107662',
    'RU000A107T19'
);
