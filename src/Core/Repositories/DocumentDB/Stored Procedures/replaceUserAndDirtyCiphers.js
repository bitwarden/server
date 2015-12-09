// Replace user document and mark all related ciphers as dirty.

function replaceUserAndDirtyCiphers(user) {
    var context = getContext();
    var collection = context.getCollection();
    var collectionLink = collection.getSelfLink();
    var response = context.getResponse();

    // Validate input.
    if (!user) {
        throw new Error('The user is undefined or null.');
    }

    getUser(function (userDoc) {
        replaceUser(userDoc, function (replacedDoc) {
            queryAndDirtyCiphers(function () {
                response.setBody(replacedDoc);
            });
        });
    });

    function getUser(callback, continuation) {
        var query = {
            query: 'SELECT * FROM root r WHERE r.id = @id',
            parameters: [{ name: '@id', value: user.id }]
        };

        var requestOptions = { continuation: continuation };
        var accepted = collection.queryDocuments(collectionLink, query, requestOptions, function (err, documents, responseOptions) {
            if (err) throw err;

            if (documents.length > 0) {
                callback(documents[0]);
            }
            else if (responseOptions.continuation) {
                getUser(responseOptions.continuation);
            }
            else {
                throw new Error('User not found.');
            }
        });

        if (!accepted) {
            throw new Error('The stored procedure timed out.');
        }
    }

    function replaceUser(userDoc, callback) {
        var accepted = collection.replaceDocument(userDoc._self, user, {}, function (err, replacedDoc) {
            if (err) throw err;

            callback(replacedDoc);
        });

        if (!accepted) {
            throw new Error('The stored procedure timed out.');
        }
    }

    function queryAndDirtyCiphers(callback, continuation) {
        var query = {
            query: 'SELECT * FROM root r WHERE r.type = @type AND r.UserId = @userId',
            parameters: [{ name: '@type', value: 'cipher' }, { name: '@userId', value: user.id }]
        };

        var requestOptions = { continuation: continuation };
        var accepted = collection.queryDocuments(collectionLink, query, requestOptions, function (err, documents, responseOptions) {
            if (err) throw err;

            if (documents.length > 0) {
                dirtyCiphers(documents, callback);
            }
            else if (responseOptions.continuation) {
                queryAndDirtyCiphers(callback, responseOptions.continuation);
            }
            else {
                callback();
            }
        });

        if (!accepted) {
            throw new Error('The stored procedure timed out.');
        }
    }

    function dirtyCiphers(documents, callback) {
        if (documents.length > 0) {
            // dirty the cipher
            documents[0].Dirty = true;

            var requestOptions = { etag: documents[0]._etag };
            var accepted = collection.replaceDocument(documents[0]._self, documents[0], requestOptions, function (err) {
                if (err) throw err;

                documents.shift();
                dirtyCiphers(documents, callback);
            });

            if (!accepted) {
                throw new Error('The stored procedure timed out.');
            }
        }
        else {
            callback();
        }
    }
}
