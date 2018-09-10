defmodule SampleServerWeb.PageController do
  use SampleServerWeb, :controller

  def index(conn, _params) do
    render conn, "index.html"
  end
end
