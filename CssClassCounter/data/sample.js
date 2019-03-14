var MainView = Backbone.View.extend({

  template: _.template($('#main').html()),

  className: 'class1 class2',

  render: function() {
    return this;
  }

});

var ChildView = Backbone.View.extend({

  template: _.template($('#child').html()),

  className: ' class2 text-white ',

  render: function() {
    return this;
  }

});