CREATE TABLE IF NOT EXISTS users (
    id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    username varchar(100),
    passwordHash varchar(256)
);

CREATE index IF NOT EXISTS users_username ON users(username);

CREATE TABLE IF NOT EXISTS messages (
    id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    senderId int,
    recipientId int,
    message varchar(10000),
    sentTime TIMESTAMP,
    unread bit
);

CREATE index IF NOT EXISTS message_senttime_user_pair ON messages(senderid, recipientid, senttime DESC);
CREATE index IF NOT EXISTS message_senttime_sender ON messages(senderid, senttime DESC);
CREATE index IF NOT EXISTS message_senttime_recipient ON messages(recipientid, senttime DESC);

CREATE TABLE IF NOT EXISTS attachments (
    id INT PRIMARY KEY GENERATED ALWAYS AS IDENTITY,
    messageId INT,
    fileName varchar(256),
    filePath varchar(256)
);

CREATE INDEX IF NOT EXISTS attachment_message ON attachments(messageId);